using BranchBooking.Application.Abstractions;
using BranchBooking.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BranchBooking.Infrastructure.Persistence.Repositories;

public sealed class ReminderCandidateReader(BranchBookingDbContext context) : IReminderCandidateReader
{
    public async Task<IReadOnlyList<ReminderCandidate>> FindDueAsync(ReminderQuery query, CancellationToken cancellationToken)
    {
        var startsAfter = query.StartsAfterUtc.ToUniversalTime();
        var startsAtOrBefore = query.StartsAtOrBeforeUtc.ToUniversalTime();
        var updatedAtOrBefore = query.UpdatedAtOrBeforeUtc.ToUniversalTime();

        return await context.Bookings
            .AsNoTracking()
            .Where(b => b.Status == BookingStatus.Confirmed
                        && b.StartsAtUtc > startsAfter
                        && b.StartsAtUtc <= startsAtOrBefore
                        && b.UpdatedAtUtc <= updatedAtOrBefore)
            .Where(b => !context.NotificationMessages.Any(m =>
                m.BookingId == b.Id
                && m.Kind == NotificationKind.Reminder
                && m.StartsAtUtc == b.StartsAtUtc))
            .OrderBy(b => b.StartsAtUtc)
            .ThenBy(b => b.Id)
            .Select(b => new ReminderCandidate(b.Id, b.StartsAtUtc))
            .Take(query.Limit)
            .ToListAsync(cancellationToken);
    }
}
