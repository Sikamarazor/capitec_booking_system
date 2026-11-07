using BranchBooking.Application.Abstractions;
using BranchBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BranchBooking.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository(BranchBookingDbContext context) : INotificationRepository
{
    public void Add(NotificationMessage message) => context.NotificationMessages.Add(message);

    public async Task<IReadOnlyList<NotificationMessage>> ListForBookingAsync(Guid bookingId, CancellationToken cancellationToken) =>
        await context.NotificationMessages
            .AsNoTracking()
            .Where(m => m.BookingId == bookingId)
            .OrderBy(m => m.CreatedAtUtc)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);
}
