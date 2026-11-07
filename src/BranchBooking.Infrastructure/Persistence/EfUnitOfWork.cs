using BranchBooking.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BranchBooking.Infrastructure.Persistence;

public sealed class EfUnitOfWork(BranchBookingDbContext context) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            context.ChangeTracker.Clear();
            throw new ConcurrencyConflictException(ex);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, DatabaseConstraints.ConfirmedSlotUnique))
        {
            context.ChangeTracker.Clear();
            throw new SlotAlreadyBookedException(ex);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, DatabaseConstraints.BookingReferenceUnique))
        {
            context.ChangeTracker.Clear();
            throw new DuplicateBookingReferenceException(ex);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, DatabaseConstraints.NotificationDedupeKeyUnique))
        {
            context.ChangeTracker.Clear();
            throw new NotificationAlreadyQueuedException(ex);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception, string constraintName) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
        && postgres.ConstraintName == constraintName;
}
