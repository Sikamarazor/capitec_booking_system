using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BranchBooking.IntegrationTests.Support;

/// <summary>
/// Hosts the real API against the test database named in <c>BRANCHBOOKING_TEST_CONNECTION</c>, with a clock the
/// test controls. The schema is applied by <see cref="PostgresFixture"/>; tests insert their own data.
/// The host is built lazily, so merely constructing this factory needs no database.
/// </summary>
public sealed class DatabaseApiFactory : WebApplicationFactory<Program>
{
    private const string Unused = "Host=localhost;Database=unused_test;Username=unused;Password=unused";

    public MutableClock Clock { get; } = new(new DateTimeOffset(2026, 11, 2, 7, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connection = Environment.GetEnvironmentVariable(PostgresFixture.ConnectionVariable);
        builder.UseSetting("ConnectionStrings:BranchBooking", string.IsNullOrWhiteSpace(connection) ? Unused : connection);
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Database:SeedOnStartup", "false");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}
