namespace ShutItDown.Core.Power;

/// <summary>Turns the machine off. Kept behind an interface so the web layer can be tested without shutting anything down.</summary>
public interface IPowerController
{
    /// <summary>When a shutdown scheduled by this app will happen, or null if none is pending.</summary>
    DateTimeOffset? ScheduledShutdown { get; }

    /// <summary>Schedules a shutdown after <paramref name="delay"/>. Replaces any shutdown that is already pending.</summary>
    void ScheduleShutdown(TimeSpan delay);

    /// <summary>Cancels a pending shutdown. Does nothing when none is pending.</summary>
    void CancelShutdown();
}
