namespace ShutItDown.Tray;

internal static class Program
{
    private const string MutexName = @"Local\ShutItDown.Tray";
    private const string ShowEventName = @"Local\ShutItDown.Tray.Show";

    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            // Already running: ask that instance to show its window, then quit.
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out var showExisting))
            {
                using (showExisting)
                    showExisting.Set();
            }
            return;
        }

        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var app = new App { ShowEvent = showEvent, StartMinimized = args.Contains(StartupRegistration.MinimizedArgument) };
        app.InitializeComponent();
        app.Run();
    }
}
