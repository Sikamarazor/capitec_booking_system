using BranchBooking.Domain.Entities;
using BranchBooking.Domain.ValueObjects;

namespace BranchBooking.Application.Abstractions;

public interface IBookingRepository
{
    /// <summary>Stages a new booking. It is written when <see cref="IUnitOfWork.SaveChangesAsync"/> runs.</summary>
    void Add(Booking booking);

    /// <summary>
    /// The booking with its branch (including opening hours) and appointment type, or <c>null</c>.
    /// The entity is tracked so that changes made to it are saved by the unit of work.
    /// </summary>
    Task<Booking?> GetByReferenceAsync(BookingReference reference, CancellationToken cancellationToken);

    /// <summary>
    /// Positions held by <b>confirmed</b> bookings at a branch whose start time is in
    /// [<paramref name="fromUtc"/>, <paramref name="toUtc"/>), ordered by start time then slot number.
    /// </summary>
    Task<IReadOnlyList<BookedPosition>> GetConfirmedPositionsAsync(
        Guid branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);
}
