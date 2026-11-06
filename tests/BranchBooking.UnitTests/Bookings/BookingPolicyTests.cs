using BranchBooking.Application.Bookings;
using BranchBooking.Domain.ValueObjects;
using BranchBooking.UnitTests.Support;
using Microsoft.Extensions.Options;
using Xunit;

namespace BranchBooking.UnitTests.Bookings;

public class BookingPolicyTests
{
    private static readonly DateTimeOffset Now = DomainTestData.Now;   // Monday 2 November 2026, 09:00 SAST

    private static readonly DateOnly Today = new(2026, 11, 2);

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void Booking_window_runs_from_today_to_the_horizon_inclusive(int daysFromToday, bool expected)
    {
        var world = new TestWorld();

        Assert.Equal(expected, world.Policy.IsDateInBookingWindow(world.Branch, Today.AddDays(daysFromToday)));
    }

    [Fact]
    public void Today_is_the_branch_local_date_not_the_utc_date()
    {
        // 22:30 UTC on 2 November is 00:30 on 3 November in South Africa.
        var world = new TestWorld(now: new DateTimeOffset(2026, 11, 2, 22, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 11, 3), world.Policy.TodayAt(world.Branch));
        Assert.False(world.Policy.IsDateInBookingWindow(world.Branch, new DateOnly(2026, 11, 2)));
    }

    [Theory]
    [InlineData(59, false)]
    [InlineData(60, true)]
    [InlineData(61, true)]
    public void A_slot_must_start_at_least_the_minimum_notice_from_now(int minutesFromNow, bool expected)
    {
        var world = new TestWorld();
        var slot = DomainTestData.NewSlot(Now.AddMinutes(minutesFromNow));

        Assert.Equal(expected, world.Policy.IsSlotBookable(slot));
    }

    [Theory]
    [InlineData(119, false)]
    [InlineData(120, true)]
    [InlineData(600, true)]
    public void A_booking_can_be_changed_until_the_cut_off(int minutesUntilStart, bool expected)
    {
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(Now.AddMinutes(minutesUntilStart));

        Assert.Equal(expected, world.Policy.CanChange(booking));
    }

    [Fact]
    public void A_cancelled_booking_cannot_be_changed()
    {
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(Now.AddDays(1));
        booking.Cancel(Now.AddHours(1));

        Assert.False(world.Policy.CanChange(booking));
    }

    [Fact]
    public void Reschedule_allowance_ends_after_the_configured_number_of_reschedules()
    {
        var world = new TestWorld(policyOptions: new BookingPolicyOptions { MaxReschedules = 2 });
        var booking = world.AddConfirmedBooking(Now.AddDays(1));
        Assert.True(world.Policy.HasRescheduleAllowance(booking));

        booking.Reschedule(DomainTestData.NewSlot(Now.AddDays(2)), 1, Now);
        Assert.True(world.Policy.HasRescheduleAllowance(booking));

        booking.Reschedule(DomainTestData.NewSlot(Now.AddDays(3)), 1, Now);
        Assert.False(world.Policy.HasRescheduleAllowance(booking));
    }

    [Fact]
    public void Default_options_are_valid()
    {
        Assert.True(new BookingPolicyOptions().IsValid());
    }

    [Theory]
    [InlineData(0, 30, 2, 3)]
    [InlineData(60, 0, 2, 3)]
    [InlineData(60, 366, 2, 3)]
    [InlineData(60, 30, -1, 3)]
    [InlineData(60, 30, 2, 21)]
    public void Out_of_range_options_are_invalid(int notice, int advance, int cutoff, int reschedules)
    {
        var options = new BookingPolicyOptions
        {
            MinimumNoticeMinutes = notice,
            MaxAdvanceDays = advance,
            ChangeCutoffHours = cutoff,
            MaxReschedules = reschedules,
        };

        Assert.False(options.IsValid());
    }
}
