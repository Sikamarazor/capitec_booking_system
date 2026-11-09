namespace BranchBooking.Application.Abstractions;

public interface IUnitOfWork
{
    /// <summary>
    /// Writes all staged changes in one transaction.
    /// </summary>
    /// <exception cref="SlotAlreadyBookedException">Another confirmed booking holds the same position.</exception>
    /// <exception cref="DuplicateBookingReferenceException">The generated reference already exists.</exception>
    /// <exception cref="ConcurrencyConflictException">Someone else changed the booking since it was loaded.</exception>
    /// <remarks>After any of these exceptions the change tracker is cleared; reload entities before retrying.</remarks>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
