using BranchBooking.Application;
using BranchBooking.Infrastructure;
using BranchBooking.Infrastructure.Notifications;
using BranchBooking.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddNotificationDispatch();
builder.Services.AddReminderScheduling();

builder.Services.AddHostedService<HeartbeatService>();
builder.Services.AddHostedService<NotificationDispatchService>();
builder.Services.AddHostedService<ReminderSchedulingService>();

await builder.Build().RunAsync();
