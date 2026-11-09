using Microsoft.Extensions.DependencyInjection;

namespace BranchBooking.Infrastructure.Persistence.Seeding;

public static class DatabaseSeeding
{
    /// <summary>Seeds demo reference data. Run after migrations.</summary>
    public static async Task SeedDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>().SeedAsync(cancellationToken);
    }
}
