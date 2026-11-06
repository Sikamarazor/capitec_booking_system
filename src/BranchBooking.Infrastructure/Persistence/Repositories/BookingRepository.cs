using BranchBooking.Application.Abstractions;
using BranchBooking.Domain.Entities;
using BranchBooking.Domain.Enums;
using BranchBooking.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace BranchBooking.Infrastructure.Persistence.Repositories;

public sealed class BookingRepository(BranchBookingDbContext context) : IBookingRepository
{
    public void Add(Booking booking) => context.Bookings.Add(booking);

    public Task<Booking?> GetByReferenceAsync(BookingReference reference, CancellationToken cancellationToken) =>
        context.Bookings
            .Include(b => b.Branch)
                .ThenInclude(branch => branch.OpeningHours)
            .Include(b => b.AppointmentType)
            .SingleOrDefaultAsync(b => b.Reference == reference, cancellationToken);

    public async Task<IReadOnlyList<BookedPosition>> GetConfirmedPositionsAsync(
        Guid branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) =>
        await context.Bookings
            .AsNoTracking()
            .Where(b => b.BranchId == branchId
                        && b.Status == BookingStatus.Confirmed
                        && b.StartsAtUtc >= fromUtc
                        && b.StartsAtUtc < toUtc)
            .OrderBy(b => b.StartsAtUtc)
            .ThenBy(b => b.SlotNumber)
            .Select(b => new BookedPosition(b.StartsAtUtc, b.SlotNumber))
            .ToListAsync(cancellationToken);
}
