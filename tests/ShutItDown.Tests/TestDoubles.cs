using ShutItDown.Core.Monitoring;
using ShutItDown.Core.Power;

namespace ShutItDown.Tests;

internal sealed class FakeClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}

internal sealed class FakePcMonitor : IPcMonitor
{
    public double Cpu { get; set; } = 50;
    public double Download { get; set; } = 5_000_000;
    public TimeSpan UserIdle { get; set; } = TimeSpan.FromMinutes(30);
    public List<OpenApp> Apps { get; } = new();
    public HashSet<string> Running { get; } = new(StringComparer.OrdinalIgnoreCase);
    public byte[]? Screen { get; set; } = [0xFF, 0xD8, 0xFF];

    public PcSnapshot GetSnapshot() => new(Cpu, 40, Download, 1000, UserIdle, TimeSpan.FromHours(3));
    public IReadOnlyList<OpenApp> GetOpenApps() => Apps;
    public bool IsProcessRunning(string processName) => Running.Contains(processName);
    public byte[]? CaptureScreenJpeg(int maxWidth) => Screen;
}

internal sealed class FakePowerController : IPowerController
{
    public List<TimeSpan> Scheduled { get; } = new();
    public int CancelCount { get; private set; }
    public DateTimeOffset? ScheduledShutdown { get; private set; }

    public void ScheduleShutdown(TimeSpan delay)
    {
        Scheduled.Add(delay);
        ScheduledShutdown = DateTimeOffset.UtcNow + delay;
    }

    public void CancelShutdown()
    {
        CancelCount++;
        ScheduledShutdown = null;
    }
}
