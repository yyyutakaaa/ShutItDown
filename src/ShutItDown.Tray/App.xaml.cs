using System.Windows;

namespace ShutItDown.Tray;

public partial class App : Application
{
    private TrayApplication? _tray;

    /// <summary>Set by <see cref="Program"/> before <see cref="Application.Run()"/>.</summary>
    public EventWaitHandle ShowEvent { get; init; } = null!;
    public bool StartMinimized { get; init; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _tray = new TrayApplication(ShowEvent, StartMinimized);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
