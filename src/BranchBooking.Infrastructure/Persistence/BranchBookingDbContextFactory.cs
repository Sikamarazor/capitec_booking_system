using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BranchBooking.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef</c> create the context without starting the web host. Not used at runtime.</summary>
public sealed class BranchBookingDbContextFactory : IDesignTimeDbContextFactory<BranchBookingDbContext>
{
    private const string Variable = "ConnectionStrings__BranchBooking";

    public BranchBookingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Set the {Variable} environment variable before running dotnet ef " +
                "(see docs/engineering/database-migrations.md).");
        }

        var options = new DbContextOptionsBuilder<BranchBookingDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new BranchBookingDbContext(options);
    }
}
