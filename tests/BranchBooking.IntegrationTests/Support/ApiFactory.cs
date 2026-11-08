using BranchBooking.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BranchBooking.IntegrationTests.Support;

/// <summary>Hosts the real API in memory with a stubbed database probe, so no database is needed.</summary>
public abstract class StubbedApiFactory(bool databaseUp) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Satisfies startup validation; the stubbed probe means the data source is never opened.
        builder.UseSetting("ConnectionStrings:BranchBooking", "Host=localhost;Database=unused;Username=unused;Password=unused");
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Database:SeedOnStartup", "false");
        builder.UseSetting("OpenApi:Enabled", "false");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDatabaseProbe>();
            services.AddSingleton<IDatabaseProbe>(new StubDatabaseProbe(databaseUp));
        });
    }
}

public sealed class ApiFactory() : StubbedApiFactory(databaseUp: true);

public sealed class DatabaseDownApiFactory() : StubbedApiFactory(databaseUp: false);
