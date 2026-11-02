using BranchBooking.Application;
using BranchBooking.Infrastructure;
using BranchBooking.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<HeartbeatService>();

await builder.Build().RunAsync();
