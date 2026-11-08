using System.Windows;
using ShutItDown.Core.Monitoring;
using ShutItDown.Core.Power;
using ShutItDown.Core.Settings;
using ShutItDown.Web;
using Forms = System.Windows.Forms;

namespace ShutItDown.Tray;

/// <summary>Owns the tray icon for as long as the app runs. The window is only opened on demand.</summary>
public sealed class TrayApplication : IDisposable
{
    private readonly WindowsPcMonitor _monitor;
    private readonly AppController _controller;
    private readonly MainWindow _window;
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly Forms.ToolStripMenuItem _toggleItem;
    private readonly RegisteredWaitHandle _showWait;
    private bool _exiting;

    public TrayApplication(EventWaitHandle showEvent, bool startMinimized)
    {
        StartupRegistration.MigrateLegacyShortcut();
        _monitor = new WindowsPcMonitor();
        _controller = new AppController(new SettingsStore(), new WindowsPowerController(), _monitor);
        _window = new MainWindow(_controller);

        _toggleItem = new Forms.ToolStripMenuItem("Stop server", null, async (_, _) => await ToggleServerAsync());
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Shutdown Server", null, (_, _) => ShowWindow());
        menu.Items.Add("Copy pairing link", null, (_, _) => CopyPhoneLink());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, async (_, _) => await ExitAsync());

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) ShowWindow();
        };

        // A second launch of the exe signals this event instead of starting another copy.
        _showWait = ThreadPool.RegisterWaitForSingleObject(
            showEvent, (_, _) => _window.Dispatcher.BeginInvoke(ShowWindow), null, Timeout.Infinite, executeOnlyOnce: false);

        _controller.StateChanged += (_, _) => _window.Dispatcher.BeginInvoke(UpdateTray);
        _controller.ShutdownRequested += (_, info) => _window.Dispatcher.BeginInvoke(() => NotifyShutdownRequested(info));
        _controller.Paired += (_, name) => _window.Dispatcher.BeginInvoke(() =>
            _trayIcon.ShowBalloonTip(4000, "Phone paired", $"{name} can now shut down this PC.", Forms.ToolTipIcon.Info));
        _controller.WatchTriggered += (_, state) => _window.Dispatcher.BeginInvoke(() =>
            _trayIcon.ShowBalloonTip(15_000, "Done, shutting down soon",
                $"The PC was set to shut down {state.Describe()}. Click here to cancel.", Forms.ToolTipIcon.Warning));
        _trayIcon.BalloonTipClicked += (_, _) => ShowWindow();
        UpdateTray();

        if (!startMinimized)
            ShowWindow();

        _ = StartServerOnLaunchAsync();
    }

    private async Task StartServerOnLaunchAsync()
    {
        try
        {
            await _controller.StartServerAsync();
        }
        catch (Exception ex)
        {
            _trayIcon.ShowBalloonTip(
                5000,
                "Shutdown Server could not start",
                $"Port {_controller.Port} may be in use by another program. Open the app to pick another port.\n{ex.Message}",
                Forms.ToolTipIcon.Error);
        }
    }

    private async Task ToggleServerAsync()
    {
        try
        {
            if (_controller.IsRunning)
                await _controller.StopServerAsync();
            else
                await _controller.StartServerAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Shutdown Server", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateTray()
    {
        _toggleItem.Text = _controller.IsRunning ? "Stop server" : "Start server";
        _trayIcon.Text = _controller.IsRunning ? "Shutdown Server - online" : "Shutdown Server - offline";
    }

    private void NotifyShutdownRequested(ShutdownScheduledInfo info)
    {
        var secondsLeft = (info.At - DateTimeOffset.UtcNow).TotalSeconds;
        var when = secondsLeft < 2 ? "now" : $"at {info.At.ToLocalTime():HH:mm:ss}";
        _trayIcon.ShowBalloonTip(
            10_000,
            "Shutdown requested",
            $"A phone ({info.Client}) is shutting down this PC {when}. Click here to cancel.",
            Forms.ToolTipIcon.Warning);
    }

    private void CopyPhoneLink()
    {
        Clipboard.SetText(_controller.PairingUrl);
        _trayIcon.ShowBalloonTip(3000, "Pairing link copied", "Open it on your phone within 10 minutes. It works once.", Forms.ToolTipIcon.Info);
    }

    private void ShowWindow()
    {
        if (_exiting) return;
        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private async Task ExitAsync()
    {
        if (_exiting) return;
        _exiting = true;
        _trayIcon.Visible = false;
        try
        {
            await _controller.DisposeAsync();
        }
        catch
        {
            // The process is ending anyway.
        }
        _window.AllowClose = true;
        _window.Close();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _showWait.Unregister(null);
        _trayIcon.Dispose();
        _monitor.Dispose();
    }
}
