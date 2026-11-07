using BranchBooking.Application.Abstractions;
using BranchBooking.Application.Notifications;
using BranchBooking.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BranchBooking.Infrastructure.Notifications;

public static class ReminderRegistration
{
    /// <summary>What the Worker needs to queue reminders. The API never calls this.</summary>
    public static IServiceCollection AddReminderScheduling(this IServiceCollection services)
    {
        services.AddOptions<ReminderOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                configuration.GetSection(ReminderOptions.SectionName).Bind(options))
            .Validate(
                options => options.IsValid(),
                "Notifications:Reminders settings are out of range; see docs/engineering/notifications.md.")
            .ValidateOnStart();

        services.AddScoped<IReminderCandidateReader, ReminderCandidateReader>();
        services.AddScoped<ScheduleReminders>();

        return services;
    }
}
