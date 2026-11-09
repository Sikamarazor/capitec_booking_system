namespace BranchBooking.IntegrationTests.Support;

/// <summary>A clock the test controls, so "now" is the same on every run.</summary>
public sealed class MutableClock(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public void Set(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
}
