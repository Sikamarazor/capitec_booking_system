using BranchBooking.Application.Abstractions;
using BranchBooking.Application.Notifications;
using BranchBooking.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BranchBooking.UnitTests.Notifications;

public class NotificationRegistrationTests
{
    private static ServiceProvider Build(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value))
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddNotificationDispatch();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static readonly (string, string)[] MailpitSettings =
    [
        ("Notifications:Channel", "Smtp"),
        ("Notifications:Smtp:Host", "mailpit"),
        ("Notifications:Smtp:Port", "1025"),
        ("Notifications:Smtp:Security", "None"),
        ("Notifications:Smtp:SenderEmail", "sender@example.com"),
    ];

    [Fact]
    public void With_no_settings_the_simulated_sender_is_used()
    {
        using var provider = Build();
        using var scope = provider.CreateScope();

        var sender = scope.ServiceProvider.GetRequiredService<INotificationSender>();

        Assert.IsType<SimulatedEmailSender>(sender);
    }

    [Fact]
    public void The_smtp_channel_with_complete_settings_uses_the_smtp_sender()
    {
        using var provider = Build(MailpitSettings);
        using var scope = provider.CreateScope();

        var sender = scope.ServiceProvider.GetRequiredService<INotificationSender>();

        Assert.IsType<SmtpEmailSender>(sender);
    }

    [Fact]
    public void The_smtp_settings_are_bound_from_configuration_including_the_security_name()
    {
        using var provider = Build(MailpitSettings);

        var smtp = provider.GetRequiredService<IOptions<SmtpOptions>>().Value;

        Assert.Equal("mailpit", smtp.Host);
        Assert.Equal(1025, smtp.Port);
        Assert.Equal(SmtpSecurity.None, smtp.Security);
    }

    [Fact]
    public void The_real_transport_is_registered()
    {
        using var provider = Build(MailpitSettings);

        Assert.IsType<MailKitSmtpTransport>(provider.GetRequiredService<ISmtpTransport>());
    }

    [Fact]
    public void The_smtp_channel_without_a_host_fails_validation_and_names_the_settings_without_leaking_values()
    {
        using var provider = Build(
            ("Notifications:Channel", "Smtp"),
            ("Notifications:Smtp:SenderEmail", "sender@example.com"),
            ("Notifications:Smtp:Username", "user"),
            ("Notifications:Smtp:Password", "hunter2-not-real"));

        var failure = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SmtpOptions>>().Value);

        Assert.Contains("Notifications:Smtp", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sender@example.com", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_simulated_channel_does_not_require_any_smtp_settings()
    {
        using var provider = Build(("Notifications:Channel", "Simulated"));

        var smtp = provider.GetRequiredService<IOptions<SmtpOptions>>().Value;

        Assert.False(smtp.IsValid());
    }

    [Fact]
    public void Out_of_range_notification_settings_fail_validation()
    {
        using var provider = Build(("Notifications:BatchSize", "0"));

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<NotificationOptions>>().Value);
    }

    [Fact]
    public void Notification_settings_are_bound_from_configuration()
    {
        using var provider = Build(
            ("Notifications:BrandName", "Test Brand"),
            ("Notifications:PollSeconds", "3"));

        var options = provider.GetRequiredService<IOptions<NotificationOptions>>().Value;

        Assert.Equal("Test Brand", options.BrandName);
        Assert.Equal(3, options.PollSeconds);
    }
}
