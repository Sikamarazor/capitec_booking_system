using BranchBooking.Domain.Entities;
using BranchBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace BranchBooking.IntegrationTests.Support;

/// <summary>
/// Shared by all database-backed tests. Applies the real migrations once to the database named in
/// <c>BRANCHBOOKING_TEST_CONNECTION</c>. When the variable is not set, database tests are skipped, not failed.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string ConnectionVariable = "BRANCHBOOKING_TEST_CONNECTION";

    private NpgsqlDataSource? _dataSource;

    public bool IsAvailable => _dataSource is not null;

    public async ValueTask InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        // Tests delete all rows, so refuse anything that does not look like a throwaway database.
        var database = new NpgsqlConnectionStringBuilder(connectionString).Database;
        if (database is null || !database.EndsWith("_test", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{ConnectionVariable} must point to a database whose name ends with '_test' " +
                $"because tests delete all data. Got '{database}'.");
        }

        _dataSource = NpgsqlDataSource.Create(connectionString);

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => _dataSource?.DisposeAsync() ?? ValueTask.CompletedTask;

    public void RequireDatabase() =>
        Assert.SkipUnless(IsAvailable, $"Set {ConnectionVariable} to a PostgreSQL database whose name ends in _test to run this test.");

    public BranchBookingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BranchBookingDbContext>()
            .UseNpgsql(_dataSource!)
            .Options;

        return new BranchBookingDbContext(options);
    }

    public async Task ResetAsync()
    {
        RequireDatabase();
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE bookings, branch_opening_hours, branches, appointment_types CASCADE");
    }

        /// <summary>Skips when there is no database; otherwise empties it and inserts one branch and one appointment type.</summary>
    public async Task<(Branch Branch, AppointmentType Type)> PrepareAsync(int slotCapacity = 2)
    {
        await ResetAsync();

        var branch = PersistenceTestData.NewBranch(slotCapacity: slotCapacity);
        var type = PersistenceTestData.NewType();

        await using var context = CreateContext();
        context.Branches.Add(branch);
        context.AppointmentTypes.Add(type);
        await context.SaveChangesAsync();

        return (branch, type);
    }

    /// <summary>The shared data source, for code under test that talks to PostgreSQL directly.</summary>
    public NpgsqlDataSource DataSource =>
        _dataSource ?? throw new InvalidOperationException($"No test database. Set {ConnectionVariable}.");

}
