using BranchBooking.Domain.Entities;

namespace BranchBooking.Application.Abstractions;

public interface INotificationQueue
{
    /// <summary>
    /// Claims up to <paramref name="batchSize"/> pending messages that are due at <paramref name="nowUtc"/>,
    /// oldest first, and leases them for <paramref name="lease"/> so no other Worker takes them. Returns the
    /// messages <b>tracked</b>, with their booking, branch and appointment type loaded.
    /// </summary>
    Task<IReadOnlyList<NotificationMessage>> ClaimDueAsync(
        DateTimeOffset nowUtc, int batchSize, TimeSpan lease, CancellationToken cancellationToken);
}
