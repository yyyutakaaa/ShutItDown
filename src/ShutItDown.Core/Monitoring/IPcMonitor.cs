namespace ShutItDown.Core.Monitoring;

/// <summary>What the PC is doing right now.</summary>
/// <param name="UserIdle">Time since the last keyboard or mouse input.</param>
public sealed record PcSnapshot(
    double CpuPercent,
    double MemoryPercent,
    double DownloadBytesPerSecond,
    double UploadBytesPerSecond,
    TimeSpan UserIdle,
    TimeSpan Uptime);

/// <summary>A program with a visible window.</summary>
public sealed record OpenApp(string ProcessName, string Title);

/// <summary>Reads live information from the PC. Implemented by the Windows app; faked in tests.</summary>
public interface IPcMonitor
{
    PcSnapshot GetSnapshot();

    IReadOnlyList<OpenApp> GetOpenApps();

    bool IsProcessRunning(string processName);

    /// <summary>A JPEG of the whole screen, scaled down to at most <paramref name="maxWidth"/> pixels wide.</summary>
    byte[]? CaptureScreenJpeg(int maxWidth);
}
