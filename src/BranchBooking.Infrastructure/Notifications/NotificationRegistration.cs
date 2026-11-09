using BranchBooking.Application.Abstractions;
using BranchBooking.Application.Notifications;
using BranchBooking.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BranchBooking.Infrastructure.Notifications;

public static class NotificationRegistration
{
    /// <summary>
    /// Everything the Worker needs to send notifications. The channel is chosen from configuration when the
    /// sender is resolved: <c>Simulated</c> (default) or <c>Smtp</c>. SMTP settings are validated at startup
    /// only when Smtp is the chosen channel, so the default setup needs none of them.
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

        services.AddOptions<SmtpOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                configuration.GetSection(SmtpOptions.SectionName).Bind(options))
            .Validate<IOptions<NotificationOptions>>(
                (options, notifications) => !notifications.Value.UsesSmtp || options.IsValid(),
                "Notifications:Channel is Smtp but Notifications:Smtp is incomplete: Host, a Port of 1 to 65535, Security, "
                + "SenderEmail, SenderName and a TimeoutSeconds of 1 to 60 are required; Username and Password go together "
                + "and need Security other than None; see docs/engineering/notifications.md.")
            .ValidateOnStart();

        services.AddSingleton<NotificationPolicy>();
        services.AddSingleton<NotificationContentBuilder>();
        services.AddScoped<INotificationQueue, NotificationQueue>();
        services.AddScoped<DispatchNotifications>();

        services.AddSingleton<SimulatedEmailSender>();
        services.AddSingleton<ISmtpTransport, MailKitSmtpTransport>();
        services.AddScoped<SmtpEmailSender>();

        services.AddScoped<INotificationSender>(provider =>
            provider.GetRequiredService<IOptions<NotificationOptions>>().Value.UsesSmtp
                ? provider.GetRequiredService<SmtpEmailSender>()
                : provider.GetRequiredService<SimulatedEmailSender>());

        return services;
    }
}
