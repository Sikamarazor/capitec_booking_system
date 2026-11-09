using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BranchBooking.Infrastructure.Persistence.Seeding;

/// <summary>
/// Inserts demo branches and appointment types that are not there yet, matched by <c>Code</c>.
/// Existing rows are never changed, so edits made after seeding are kept. Safe to run repeatedly
/// and from several instances at once.
/// </summary>
public sealed class ReferenceDataSeeder(BranchBookingDbContext context, ILogger<ReferenceDataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var existingTypeCodes = await context.AppointmentTypes.Select(t => t.Code).ToListAsync(cancellationToken);
        var existingBranchCodes = await context.Branches.Select(b => b.Code).ToListAsync(cancellationToken);

        var newTypes = SeedData.AppointmentTypes().Where(t => !existingTypeCodes.Contains(t.Code)).ToList();
        var newBranches = SeedData.Branches().Where(b => !existingBranchCodes.Contains(b.Code)).ToList();

        if (newTypes.Count == 0 && newBranches.Count == 0)
        {
            logger.LogInformation("Reference data is already present; nothing to seed");
            return;
        }

        context.AppointmentTypes.AddRange(newTypes);
        context.Branches.AddRange(newBranches);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Seeded {TypeCount} appointment type(s) and {BranchCount} branch(es)", newTypes.Count, newBranches.Count);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another instance seeded the same codes between our check and our insert; its data is what we wanted.
            context.ChangeTracker.Clear();
            logger.LogInformation("Reference data was seeded concurrently by another instance; continuing");
        }
    }
}
