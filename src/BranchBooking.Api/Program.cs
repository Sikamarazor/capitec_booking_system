using BranchBooking.Api.Configuration;
using BranchBooking.Api.Middleware;
using BranchBooking.Application;
using BranchBooking.Infrastructure;
using BranchBooking.Infrastructure.Persistence;
using BranchBooking.Infrastructure.Persistence.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.AddServerHeader = false;
    kestrel.Limits.MaxRequestBodySize = 16 * 1024;   // Every request body here is a few hundred bytes.
});

builder.Services.AddApplication();
builder.Services.AddBookingPolicy(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddControllers();
builder.Services.AddApiProblemDetails();
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddApiCors(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

// Off by default. Development and the Docker demo stack turn it on; a real production
// deployment applies migrations as a release step (docs/engineering/database-migrations.md).
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateDatabaseAsync();
}

if (app.Configuration.GetValue<bool>("Database:SeedOnStartup"))
{
    await app.Services.SeedDatabaseAsync();
}

// TLS is terminated by the reverse proxy (nginx in Docker, or the hosting platform), so no HTTPS redirect here.
// When the proxy forwards the client address, set ASPNETCORE_FORWARDEDHEADERS_ENABLED=true (docs/api/README.md);
// the framework then adds the forwarded-headers middleware ahead of everything below.
app.UseRouting();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseRateLimiter();

if (app.Configuration.GetValue<bool>("OpenApi:Enabled"))
{
    app.MapOpenApi();   // /openapi/v1.json
}

app.MapControllers();

app.Run();

// Required so the integration tests can reference the entry point with WebApplicationFactory<Program>.
public partial class Program { }
