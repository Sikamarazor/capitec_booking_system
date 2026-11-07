using BranchBooking.Application.Abstractions;
using BranchBooking.Application.Notifications;
using BranchBooking.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BranchBooking.Infrastructure.Notifications;

public static class NotificationRegistration
{
    /// <summary>
    /// Everything the Worker needs to send notifications. Call it from hosts that dispatch (the Worker, and tests
    /// that stand in for it). The API only queues messages, so it does not call this.
    /// </summary>
    public static IServiceCollection AddNotificationDispatch(this IServiceCollection services)
    {
        services.AddOptions<NotificationOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                configuration.GetSection(NotificationOptions.SectionName).Bind(options))
            .Validate(
                options => options.IsValid(),
                "Notifications settings are missing or out of range; see docs/engineering/notifications.md.")
            .ValidateOnStart();

        services.AddSingleton<NotificationPolicy>();
        services.AddSingleton<NotificationContentBuilder>();
        services.AddScoped<INotificationQueue, NotificationQueue>();
        services.AddScoped<DispatchNotifications>();

        services.AddSingleton<SimulatedEmailSender>();
        services.AddScoped<INotificationSender>(sp => sp.GetRequiredService<SimulatedEmailSender>());

        return services;
    }
}
