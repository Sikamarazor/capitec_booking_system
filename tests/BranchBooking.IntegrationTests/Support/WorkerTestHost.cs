using BranchBooking.Application;
using BranchBooking.Application.Bookings;
using BranchBooking.Infrastructure;
using BranchBooking.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BranchBooking.IntegrationTests.Support;

/// <summary>The Worker's dependency graph, on the test database and a clock the test controls.</summary>
internal static class WorkerTestHost
{
    public static ServiceProvider Build(
        TimeProvider clock,
        Action<IServiceCollection>? configure = null,
        params (string Key, string Value)[] settings)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:BranchBooking"] =
                Environment.GetEnvironmentVariable(PostgresFixture.ConnectionVariable)
                ?? "Host=localhost;Database=unused_test;Username=unused;Password=unused",
        };

        foreach (var (key, value) in settings)
        {
            values[key] = value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(clock);
        services.AddOptions<BookingPolicyOptions>();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddNotificationDispatch();

        configure?.Invoke(services);

        return services.BuildServiceProvider(validateScopes: true);
    }
}
