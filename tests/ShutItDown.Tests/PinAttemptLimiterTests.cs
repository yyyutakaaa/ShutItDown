using ShutItDown.Core.Security;

namespace ShutItDown.Tests;

public class PinAttemptLimiterTests
{
    private readonly FakeClock _clock = new();

    [Fact]
    public void Locks_out_after_max_failures_and_unlocks_after_the_lockout()
    {
        var limiter = new PinAttemptLimiter(maxFailures: 3, lockout: TimeSpan.FromMinutes(1), clock: _clock);

        Assert.Equal(2, limiter.RegisterFailure("phone"));
        Assert.Equal(1, limiter.RegisterFailure("phone"));
        Assert.Equal(TimeSpan.Zero, limiter.GetLockoutRemaining("phone"));
        Assert.Equal(0, limiter.RegisterFailure("phone"));
        Assert.Equal(TimeSpan.FromMinutes(1), limiter.GetLockoutRemaining("phone"));

        _clock.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal(TimeSpan.FromSeconds(1), limiter.GetLockoutRemaining("phone"));

        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.Zero, limiter.GetLockoutRemaining("phone"));
        Assert.Equal(2, limiter.RegisterFailure("phone")); // counting starts over
    }

    [Fact]
    public void Clients_are_tracked_separately()
    {
        var limiter = new PinAttemptLimiter(maxFailures: 1, clock: _clock);

        limiter.RegisterFailure("attacker");

        Assert.True(limiter.GetLockoutRemaining("attacker") > TimeSpan.Zero);
        Assert.Equal(TimeSpan.Zero, limiter.GetLockoutRemaining("owner"));
    }

    [Fact]
    public void Reset_clears_failures()
    {
        var limiter = new PinAttemptLimiter(maxFailures: 3, clock: _clock);
        limiter.RegisterFailure("phone");
        limiter.RegisterFailure("phone");

        limiter.Reset("phone");

        Assert.Equal(2, limiter.RegisterFailure("phone"));
    }
}
