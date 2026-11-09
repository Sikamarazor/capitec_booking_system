using BranchBooking.Application.Notifications;
using Xunit;

namespace BranchBooking.UnitTests.Notifications;

public class ReminderOptionsTests
{
    [Fact]
    public void The_defaults_are_valid_and_enabled()
    {
        var options = new ReminderOptions();

        Assert.True(options.IsValid());
        Assert.True(options.Enabled);
    }

    [Fact]
    public void The_windows_are_exposed_as_time_spans()
    {
        var options = new ReminderOptions { LeadHours = 48, MinimumLeadMinutes = 90, MinimumGapHours = 6 };

        Assert.Equal(TimeSpan.FromHours(48), options.Lead);
        Assert.Equal(TimeSpan.FromMinutes(90), options.MinimumLead);
        Assert.Equal(TimeSpan.FromHours(6), options.MinimumGap);
    }

    [Theory]
    [InlineData(1, 60, 4, 60, 100)]      // lead too short
    [InlineData(169, 60, 4, 60, 100)]    // lead too long
    [InlineData(24, -1, 4, 60, 100)]     // negative minimum lead
    [InlineData(24, 1440, 4, 60, 100)]   // minimum lead as long as the lead
    [InlineData(24, 60, -1, 60, 100)]    // negative gap
    [InlineData(24, 60, 73, 60, 100)]    // gap too long
    [InlineData(24, 60, 4, 5, 100)]      // scan too frequent
    [InlineData(24, 60, 4, 3601, 100)]   // scan too rare
    [InlineData(24, 60, 4, 60, 0)]       // empty batch
    [InlineData(24, 60, 4, 60, 1001)]    // batch too large
    public void Out_of_range_settings_are_invalid(int leadHours, int minimumLeadMinutes, int gapHours, int scanSeconds, int batch)
    {
        var options = new ReminderOptions
        {
            LeadHours = leadHours,
            MinimumLeadMinutes = minimumLeadMinutes,
            MinimumGapHours = gapHours,
            ScanIntervalSeconds = scanSeconds,
            BatchSize = batch,
        };

        Assert.False(options.IsValid());
    }
}
