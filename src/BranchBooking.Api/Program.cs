using BranchBooking.Application;
using BranchBooking.Infrastructure;
using BranchBooking.Infrastructure.Persistence;
using BranchBooking.Infrastructure.Persistence.Seeding;
using BranchBooking.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddBookingPolicy(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

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
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseStatusCodePages();
app.MapControllers();

app.Run();

// Required so the integration tests can reference the entry point with WebApplicationFactory<Program>.
public partial class Program { }
