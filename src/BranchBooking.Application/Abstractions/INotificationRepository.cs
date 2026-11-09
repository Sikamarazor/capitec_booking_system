using BranchBooking.Domain.Entities;

namespace BranchBooking.Application.Abstractions;

public interface INotificationRepository
{
    /// <summary>Stages a message. It is written, together with any booking change, when the unit of work saves.</summary>
    void Add(NotificationMessage message);

    /// <summary>The messages of one booking, oldest first. Not tracked.</summary>
    Task<IReadOnlyList<NotificationMessage>> ListForBookingAsync(Guid bookingId, CancellationToken cancellationToken);
}
