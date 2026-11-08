using System.Net.Http;
using ShutItDown.Core.Monitoring;
using ShutItDown.Core.Networking;
using ShutItDown.Core.Pairing;
using ShutItDown.Core.Power;
using ShutItDown.Core.Security;
using ShutItDown.Core.Settings;
using ShutItDown.Core.Updates;
using ShutItDown.Web;

namespace ShutItDown.Tray;

/// <summary>The app's state and actions. The tray icon and the window only call into this class.</summary>
public sealed class AppController : IAsyncDisposable
{
    private readonly SettingsStore _store;
    private readonly PinAttemptLimiter _limiter;
    private readonly ShutdownServer _server;
    private readonly object _saveLock = new();
    private AppSettings _settings;

    public AppController(SettingsStore store, IPowerController power, IPcMonitor monitor)
    {
        _store = store;
        _settings = store.Load();
        _limiter = new PinAttemptLimiter(_settings.MaxPinAttempts);
        Power = power;
        Monitor = monitor;
        Devices = new DeviceRegistry(_settings.PairedDevices, Save);
        Pairing = new PairingCode();
        Pairing.Changed += (_, _) => OnStateChanged();

        Watcher = new ShutdownWatcher(monitor, power, () => TimeSpan.FromSeconds(_settings.GraceSeconds));
        Watcher.Changed += (_, _) => OnStateChanged();
        Watcher.Triggered += (_, state) => WatchTriggered?.Invoke(this, state);
        _watchTimer = new Timer(_ => Watcher.Tick(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

        _server = new ShutdownServer(new ShutdownApiDependencies(power, () => _settings.PinHash, _limiter, Devices, Pairing)
        {
            GetGracePeriod = () => TimeSpan.FromSeconds(_settings.GraceSeconds),
            OnShutdownScheduled = OnShutdownScheduled,
            OnPaired = name => Paired?.Invoke(this, name),
            Monitor = monitor,
            Watcher = Watcher,
            AllowScreenPreview = () => _settings.AllowScreenPreview,
        });
    }

    private readonly Timer _watchTimer;

    /// <summary>A "shut down when done" condition was met. Raised on a background thread.</summary>
    public event EventHandler<WatchState>? WatchTriggered;

    public IPcMonitor Monitor { get; }
    public ShutdownWatcher Watcher { get; }
    public bool AllowScreenPreview => _settings.AllowScreenPreview;

    public void SetAllowScreenPreview(bool allow) => Update(s => s.AllowScreenPreview = allow);

    // --- Updates -------------------------------------------------------------

    /// <summary>The GitHub repository whose releases hold new versions.</summary>
    public const string Repository = "yyyutakaaa/shutitdown";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public UpdateChecker Updates { get; } = new(Http, Repository);

    public Version CurrentVersion { get; } = ReadVersion();

    private static Version ReadVersion()
    {
#if DEBUG
        // Debug builds only: SHUTDOWNSERVER_PRETEND_VERSION=2.0.0 makes the update screen show up for testing.
        if (Version.TryParse(Environment.GetEnvironmentVariable("SHUTDOWNSERVER_PRETEND_VERSION"), out var pretend))
            return UpdateChecker.Normalize(pretend);
#endif
        return UpdateChecker.Normalize(typeof(AppController).Assembly.GetName().Version ?? new Version(0, 0, 0));
    }

    public bool CheckForUpdatesAtStartup => _settings.CheckForUpdates;

    /// <summary>A newer version was found and should be offered. May be raised on any thread.</summary>
    public event EventHandler<UpdateInfo>? UpdateFound;

    public void SetCheckForUpdates(bool check) => Update(s => s.CheckForUpdates = check);

    /// <summary>True once, on the first start after an update. Remembers the current version.</summary>
    public bool TakeJustUpdated()
    {
        var current = CurrentVersion.ToString();
        var updated = _settings.LastRunVersion is { } last && last != current;
        if (_settings.LastRunVersion != current)
            Update(s => s.LastRunVersion = current);
        return updated;
    }

    public void SkipVersion(Version version) => Update(s => s.SkippedVersion = version.ToString());

    /// <summary>
    /// Asks GitHub for a newer version and raises <see cref="UpdateFound"/> when there is one.
    /// A skipped version is only offered again when <paramref name="manual"/> is true.
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdatesAsync(bool manual)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var update = await Updates.CheckAsync(CurrentVersion, timeout.Token);
        if (update is not null && (manual || _settings.SkippedVersion != update.Version.ToString()))
            UpdateFound?.Invoke(this, update);
        return update;
    }


    /// <summary>Something the UI shows has changed. May be raised on a background thread.</summary>
    public event EventHandler? StateChanged;

    /// <summary>A phone scheduled a shutdown and the user wants to be notified. Raised on a background thread.</summary>
    public event EventHandler<ShutdownScheduledInfo>? ShutdownRequested;

    /// <summary>A phone paired; the argument is its name. Raised on a background thread.</summary>
    public event EventHandler<string>? Paired;

    public IPowerController Power { get; }
    public DeviceRegistry Devices { get; }
    public PairingCode Pairing { get; }
    public bool IsRunning => _server.IsRunning;
    public int Port => _settings.Port;
    public bool HasPin => _settings.PinHash is not null;
    public int GraceSeconds => _settings.GraceSeconds;
    public bool NotifyOnRequest => _settings.NotifyOnRequest;
    public int MaxPinAttempts => _settings.MaxPinAttempts;
    public bool ServerHintDismissed => _settings.ServerHintDismissed;
    public bool PreferTailscale => _settings.PreferTailscale;

    public PhoneAddress Address => NetworkInfo.GetPhoneAddress(_settings.PreferTailscale);

    /// <summary>Base address of the phone page, without any secret.</summary>
    public string PhoneUrl => $"http://{Address.Host}:{_settings.Port}/";

    /// <summary>What the QR code holds: the phone page plus the one-time pairing code.</summary>
    public string PairingUrl => $"{PhoneUrl}#pair={Pairing.Current}";

    public async Task StartServerAsync()
    {
        await _server.StartAsync(_settings.Port);
        OnStateChanged();
    }

    public async Task StopServerAsync()
    {
        await _server.StopAsync();
        OnStateChanged();
    }

    public void SetPin(string pin) => Update(s => s.PinHash = PinHasher.Hash(pin));

    public void RemovePin() => Update(s => s.PinHash = null);

    public void RemoveDevice(string id)
    {
        if (Devices.Remove(id))
            OnStateChanged();
    }

    public void SetPreferTailscale(bool prefer) => Update(s => s.PreferTailscale = prefer);

    public void SetGraceSeconds(int seconds)
    {
        if (seconds is < 0 or > AppSettings.MaxGraceSeconds)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        Update(s => s.GraceSeconds = seconds);
    }

    public void SetNotifyOnRequest(bool notify) => Update(s => s.NotifyOnRequest = notify);

    public void SetMaxPinAttempts(int attempts)
    {
        _limiter.MaxFailures = attempts;
        Update(s => s.MaxPinAttempts = attempts);
    }

    public void DismissServerHint() => Update(s => s.ServerHintDismissed = true);

    /// <summary>Saves the new port and restarts the server on it if it was running.</summary>
    public async Task ChangePortAsync(int port)
    {
        if (!AppSettings.IsValidPort(port))
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1024 and 65535.");
        if (port == _settings.Port)
            return;

        var wasRunning = IsRunning;
        await _server.StopAsync();
        Update(s => s.Port = port);
        if (wasRunning)
            await StartServerAsync();
    }

    /// <summary>
    /// Restores every setting to its default, removes the PIN and all paired phones,
    /// and restarts the server if the port changed.
    /// </summary>
    public async Task ResetSettingsAsync()
    {
        var defaults = new AppSettings
        {
            ServerHintDismissed = _settings.ServerHintDismissed,
            // The device registry holds on to this list, so empty it instead of replacing it.
            PairedDevices = _settings.PairedDevices,
        };
        var portChanged = defaults.Port != _settings.Port;
        var wasRunning = IsRunning;

        if (portChanged)
            await _server.StopAsync();

        _settings = defaults;
        Devices.RemoveAll();
        _limiter.MaxFailures = defaults.MaxPinAttempts;
        Pairing.Renew();
        Save();

        if (portChanged && wasRunning)
            await _server.StartAsync(_settings.Port);
        OnStateChanged();
    }

    public void CancelShutdown()
    {
        Power.CancelShutdown();
        OnStateChanged();
    }

    public async ValueTask DisposeAsync()
    {
        await _watchTimer.DisposeAsync();
        await _server.DisposeAsync();
    }

    private void Update(Action<AppSettings> change)
    {
        change(_settings);
        Save();
        OnStateChanged();
    }

    // Called from server threads too (pairing, "last used"), so writes are serialized.
    private void Save()
    {
        lock (_saveLock) _store.Save(_settings.Snapshot(Devices.Devices));
    }

    private void OnShutdownScheduled(ShutdownScheduledInfo info)
    {
        OnStateChanged();
        if (_settings.NotifyOnRequest)
            ShutdownRequested?.Invoke(this, info);
    }

    private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
