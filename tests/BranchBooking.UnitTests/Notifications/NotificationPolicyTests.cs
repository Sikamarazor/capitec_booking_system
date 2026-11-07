using BranchBooking.Application.Notifications;
using Microsoft.Extensions.Options;
using Xunit;

namespace BranchBooking.UnitTests.Notifications;

public class NotificationPolicyTests
{
    private static NotificationPolicy Policy(NotificationOptions? options = null) =>
        new(Options.Create(options ?? new NotificationOptions()));

    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    [InlineData(4, 240)]
    [InlineData(7, 1920)]
    public void The_delay_doubles_after_each_failure(int failedAttempt, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), Policy().RetryDelay(failedAttempt));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(50)]
    [InlineData(int.MaxValue)]
    public void The_delay_never_exceeds_the_cap(int failedAttempt)
    {
        Assert.Equal(TimeSpan.FromSeconds(3600), Policy().RetryDelay(failedAttempt));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void A_nonsense_attempt_number_is_treated_as_the_first(int failedAttempt)
    {
        Assert.Equal(TimeSpan.FromSeconds(30), Policy().RetryDelay(failedAttempt));
    }

    [Fact]
    public void Settings_are_exposed_as_configured()
    {
        var policy = Policy(new NotificationOptions { BatchSize = 7, MaxAttempts = 3, LeaseSeconds = 120 });

        Assert.Equal(7, policy.BatchSize);
        Assert.Equal(3, policy.MaxAttempts);
        Assert.Equal(TimeSpan.FromMinutes(2), policy.Lease);
    }
}
