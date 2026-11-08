using ShutItDown.Core.Monitoring;

namespace ShutItDown.Tests;

public class UnsavedWorkDetectorTests
{
    [Theory]
    [InlineData("*notes.txt - Notepad")]
    [InlineData("notes.txt* - Notepad++")]
    [InlineData("● Program.cs - shutitdown - Visual Studio Code")]
    [InlineData("photo.psd @ 66,7% (Layer 1, RGB/8*)")]
    [InlineData("Untitled - Notepad")]
    [InlineData("Document1 - Word")]
    [InlineData("Book2 - Excel")]
    [InlineData("Naamloos - Kladblok")]
    public void Flags_titles_that_look_unsaved(string title) =>
        Assert.NotNull(UnsavedWorkDetector.Reason(title));

    [Theory]
    [InlineData("notes.txt - Notepad")]
    [InlineData("Report final.docx - Word")]
    [InlineData("YouTube - Google Chrome")]
    [InlineData("Shutdown Server")]
    [InlineData("Spotify Premium")]
    [InlineData("")]
    public void Leaves_saved_or_normal_windows_alone(string title) =>
        Assert.Null(UnsavedWorkDetector.Reason(title));

    [Fact]
    public void Find_returns_only_the_suspicious_apps()
    {
        var apps = new[]
        {
            new OpenApp("notepad", "*todo.txt - Notepad"),
            new OpenApp("chrome", "Inbox - Gmail - Google Chrome"),
        };

        var warning = Assert.Single(UnsavedWorkDetector.Find(apps));
        Assert.Equal("notepad", warning.ProcessName);
    }
}

public class ShutdownWatcherTests
{
    private readonly FakeClock _clock = new();
    private readonly FakePcMonitor _monitor = new();
    private readonly FakePowerController _power = new();
    private readonly ShutdownWatcher _watcher;
    private readonly List<WatchState> _triggered = new();

    public ShutdownWatcherTests()
    {
        _watcher = new ShutdownWatcher(_monitor, _power, clock: _clock);
        _watcher.Triggered += (_, state) => _triggered.Add(state);
    }

    private void Wait(TimeSpan time)
    {
        for (var t = TimeSpan.Zero; t < time; t += TimeSpan.FromSeconds(5))
        {
            _clock.Advance(TimeSpan.FromSeconds(5));
            _watcher.Tick();
        }
    }

    [Fact]
    public void Idle_waits_until_the_cpu_has_been_quiet_long_enough()
    {
        _watcher.Start(new WatchRequest(WatchKind.Idle, Minutes: 10));

        Wait(TimeSpan.FromMinutes(20)); // still rendering
        Assert.Empty(_power.Scheduled);

        _monitor.Cpu = 3;
        Wait(TimeSpan.FromMinutes(9));
        Assert.Empty(_power.Scheduled);

        Wait(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(5)); // quiet time counts from the first quiet sample
        Assert.Equal([ShutdownWatcher.MinimumWarning], _power.Scheduled);
        Assert.Single(_triggered);
        Assert.Null(_watcher.Current);
    }

    [Fact]
    public void A_busy_moment_restarts_the_quiet_timer()
    {
        _watcher.Start(new WatchRequest(WatchKind.Idle, Minutes: 10));
        _monitor.Cpu = 3;
        Wait(TimeSpan.FromMinutes(8));

        _monitor.Cpu = 80;
        Wait(TimeSpan.FromSeconds(5));
        _monitor.Cpu = 3;
        Wait(TimeSpan.FromMinutes(8));

        Assert.Empty(_power.Scheduled);
    }

    [Fact]
    public void Never_shuts_down_while_someone_is_using_the_pc()
    {
        _watcher.Start(new WatchRequest(WatchKind.Network, Minutes: 5));
        _monitor.Download = 0;
        _monitor.UserIdle = TimeSpan.FromSeconds(10);

        Wait(TimeSpan.FromMinutes(30));

        Assert.Empty(_power.Scheduled);
    }

    [Fact]
    public void Network_watch_fires_after_downloads_stop()
    {
        _watcher.Start(new WatchRequest(WatchKind.Network, Minutes: 5));
        Wait(TimeSpan.FromMinutes(10));
        Assert.Empty(_power.Scheduled);

        _monitor.Download = 1_000;
        Wait(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(5));

        Assert.Single(_power.Scheduled);
    }

    [Fact]
    public void App_watch_fires_when_the_app_closes()
    {
        _monitor.Running.Add("game");
        _watcher.Start(new WatchRequest(WatchKind.AppExit, 0, "game"));
        Wait(TimeSpan.FromMinutes(1));
        Assert.Empty(_power.Scheduled);

        _monitor.Running.Clear();
        Wait(TimeSpan.FromSeconds(5));

        Assert.Single(_power.Scheduled);
    }

    [Fact]
    public void App_watch_needs_a_running_app()
    {
        Assert.Throws<ArgumentException>(() => _watcher.Start(new WatchRequest(WatchKind.AppExit, 0, "notrunning")));
        Assert.Throws<ArgumentException>(() => _watcher.Start(new WatchRequest(WatchKind.AppExit, 0, null)));
    }

    [Fact]
    public void Longer_grace_period_wins_over_the_minimum_warning()
    {
        var watcher = new ShutdownWatcher(_monitor, _power, () => TimeSpan.FromMinutes(2), _clock);
        watcher.Start(new WatchRequest(WatchKind.Idle, Minutes: 1));
        _monitor.Cpu = 1;

        for (var i = 0; i < 20; i++)
        {
            _clock.Advance(TimeSpan.FromSeconds(5));
            watcher.Tick();
        }

        Assert.Equal([TimeSpan.FromMinutes(2)], _power.Scheduled);
    }

    [Fact]
    public void Cancel_stops_waiting()
    {
        _watcher.Start(new WatchRequest(WatchKind.Idle, Minutes: 1));
        _watcher.Cancel();
        _monitor.Cpu = 1;

        Wait(TimeSpan.FromMinutes(5));

        Assert.Empty(_power.Scheduled);
    }
}
