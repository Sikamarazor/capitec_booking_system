namespace BranchBooking.Domain.Enums;

public enum NotificationStatus
{
    /// <summary>Waiting to be sent, now or at <c>NextAttemptAtUtc</c>.</summary>
    Pending = 1,

    Sent = 2,

    /// <summary>Gave up: a permanent error, or the attempts ran out.</summary>
    Failed = 3,

    /// <summary>No longer true when its turn came (booking cancelled, moved or past), so nothing was sent.</summary>
    Skipped = 4,
}
