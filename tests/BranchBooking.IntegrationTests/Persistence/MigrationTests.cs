using BranchBooking.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BranchBooking.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public class MigrationTests(PostgresFixture db)
{
    [Fact]
    public async Task Migrations_are_applied_and_the_model_has_no_pending_changes()
    {
        var ct = TestContext.Current.CancellationToken;
        db.RequireDatabase();
        await using var context = db.CreateContext();

        // MigrateAsync (run by the fixture) already throws if the model differs from the snapshot.
        var pending = await context.Database.GetPendingMigrationsAsync(ct);
        var applied = await context.Database.GetAppliedMigrationsAsync(ct);

        Assert.Empty(pending);
        Assert.Contains(applied, name => name.EndsWith("_InitialCreate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task All_expected_tables_exist()
    {
        var ct = TestContext.Current.CancellationToken;
        db.RequireDatabase();
        await using var context = db.CreateContext();

        var count = await context.Database
            .SqlQuery<int>($"""
                SELECT count(*)::int AS "Value"
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_name IN ('appointment_types', 'branches', 'branch_opening_hours', 'bookings')
                """)
            .SingleAsync(ct);

        Assert.Equal(4, count);
    }
}
