using Microsoft.Extensions.Options;

namespace BranchBooking.Application.Notifications;

public sealed class NotificationPolicy(IOptions<NotificationOptions> options)
{
    private readonly NotificationOptions _options = options.Value;

    public int BatchSize => _options.BatchSize;

    public int MaxAttempts => _options.MaxAttempts;

    public TimeSpan Lease => TimeSpan.FromSeconds(_options.LeaseSeconds);

    /// <summary>The wait after the <paramref name="failedAttemptNumber"/>th failed attempt: base, 2x base, 4x base, ... up to the cap.</summary>
    public TimeSpan RetryDelay(int failedAttemptNumber)
    {
        var doublings = Math.Clamp(failedAttemptNumber, 1, 30) - 1;
        var seconds = Math.Min(_options.RetryBaseSeconds * Math.Pow(2, doublings), _options.RetryMaxSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}
