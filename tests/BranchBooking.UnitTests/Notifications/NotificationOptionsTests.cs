using BranchBooking.Application.Notifications;
using Xunit;

namespace BranchBooking.UnitTests.Notifications;

public class NotificationOptionsTests
{
    [Fact]
    public void The_defaults_are_valid_and_simulated()
    {
        var options = new NotificationOptions();

        Assert.True(options.IsValid());
        Assert.False(options.UsesBrevo);
    }

    [Theory]
    [InlineData("Brevo")]
    [InlineData("brevo")]
    [InlineData("simulated")]
    public void Channel_names_are_case_insensitive(string channel)
    {
        Assert.True(new NotificationOptions { Channel = channel }.IsValid());
    }

    [Fact]
    public void Brevo_is_recognised()
    {
        Assert.True(new NotificationOptions { Channel = "BREVO" }.UsesBrevo);
    }

    [Theory]
    [InlineData("Smtp", "BranchBooking", "http://localhost:8080", "/manage", 20, 5, 30, 3600, 300)]
    [InlineData("Simulated", "", "http://localhost:8080", "/manage", 20, 5, 30, 3600, 300)]
    [InlineData("Simulated", "BranchBooking", "ftp://example.com", "/manage", 20, 5, 30, 3600, 300)]
    [InlineData("Simulated", "BranchBooking", "not a url", "/manage", 20, 5, 30, 3600, 300)]
    [InlineData("Simulated", "BranchBooking", "http://localhost:8080", "manage", 20, 5, 30, 3600, 300)]
    [InlineData("Simulated", "BranchBooking", "http://localhost:8080", "/manage", 0, 5, 30, 3600, 300)]
    [InlineData("Simulated", "BranchBooking", "http://localhost:8080", "/manage", 20, 0, 30, 3600, 300)]
    [InlineData("Simulated", "BranchBooking", "http://localhost:8080", "/manage", 20, 5, 0, 3600, 300)]
    [InlineData("Simulated", "BranchBooking", "http://localhost:8080", "/manage", 20, 5, 60, 30, 300)]
    [InlineData("Simulated", "BranchBooking", "http://localhost:8080", "/manage", 20, 5, 30, 3600, 5)]
    public void Out_of_range_settings_are_invalid(
        string channel, string brand, string baseUrl, string manage, int batch, int attempts, int retryBase, int retryMax, int lease)
    {
        var options = new NotificationOptions
        {
            Channel = channel,
            BrandName = brand,
            PublicBaseUrl = baseUrl,
            ManagePath = manage,
            BatchSize = batch,
            MaxAttempts = attempts,
            RetryBaseSeconds = retryBase,
            RetryMaxSeconds = retryMax,
            LeaseSeconds = lease,
        };

        Assert.False(options.IsValid());
    }
}
