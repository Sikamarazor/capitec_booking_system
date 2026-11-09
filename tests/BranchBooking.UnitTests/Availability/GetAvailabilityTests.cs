using BranchBooking.Application.Common;
using BranchBooking.UnitTests.Support;
using Xunit;

namespace BranchBooking.UnitTests.Availability;

public class GetAvailabilityTests
{
    private static readonly DateOnly Tuesday = new(2026, 11, 3);
    private static readonly DateOnly Monday = new(2026, 11, 2);

    [Fact]
    public async Task A_future_day_offers_every_slot_with_full_capacity()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(slotCapacity: 2);

        var result = await world.GetAvailability().ExecuteAsync(world.Branch.Id, Tuesday, ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(18, result.Value.Slots.Count);
        Assert.All(result.Value.Slots, s =>
        {
            Assert.Equal(2, s.Remaining);
            Assert.True(s.IsAvailable);
        });
        Assert.Equal("Africa/Johannesburg", result.Value.TimeZoneId);
    }

    [Fact]
    public async Task Local_start_is_shown_on_the_branch_clock()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();

        var result = await world.GetAvailability().ExecuteAsync(world.Branch.Id, Tuesday, ct);

        var first = result.Value.Slots[0];
        Assert.Equal(TestWorld.TuesdayEightAmUtc, first.StartsAtUtc);
        Assert.Equal("08:00", first.LocalStart);
        Assert.Equal("16:30", result.Value.Slots[^1].LocalStart);
    }

    [Fact]
    public async Task Confirmed_bookings_reduce_the_remaining_places_and_full_slots_are_marked_unavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(slotCapacity: 2);
        world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc, slotNumber: 1);
        world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc.AddMinutes(30), slotNumber: 1);
        world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc.AddMinutes(30), slotNumber: 2);

        var result = await world.GetAvailability().ExecuteAsync(world.Branch.Id, Tuesday, ct);

        var slots = result.Value.Slots;
        Assert.Equal(1, slots[0].Remaining);
        Assert.True(slots[0].IsAvailable);
        Assert.Equal(0, slots[1].Remaining);
        Assert.False(slots[1].IsAvailable);
        Assert.Equal(2, slots[2].Remaining);
    }

    [Fact]
    public async Task Cancelled_bookings_do_not_use_up_places()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(slotCapacity: 1);
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        booking.Cancel(world.Now.AddHours(1));

        var result = await world.GetAvailability().ExecuteAsync(world.Branch.Id, Tuesday, ct);

        Assert.Equal(1, result.Value.Slots[0].Remaining);
    }

    [Fact]
    public async Task Today_leaves_out_slots_inside_the_minimum_notice()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();   // now is Monday 09:00 SAST; with 60 minutes' notice the first slot is 10:00

        var result = await world.GetAvailability().ExecuteAsync(world.Branch.Id, Monday, ct);

        Assert.Equal(14, result.Value.Slots.Count);
        Assert.Equal("10:00", result.Value.Slots[0].LocalStart);
        Assert.Equal("16:30", result.Value.Slots[^1].LocalStart);
    }

    [Fact]
    public async Task A_closed_day_has_no_slots()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();

        var result = await world.GetAvailability().ExecuteAsync(world.Branch.Id, new DateOnly(2026, 11, 8), ct);   // Sunday

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Slots);
    }

    [Fact]
    public async Task An_unknown_branch_is_not_found()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();

        var result = await world.GetAvailability().ExecuteAsync(Guid.CreateVersion7(), Tuesday, ct);

        Assert.False(result.IsSuccess);
        Assert.Equal("branch_not_found", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task An_inactive_branch_is_not_found()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        world.Branch.Deactivate();

        var result = await world.GetAvailability().ExecuteAsync(world.Branch.Id, Tuesday, ct);

        Assert.Equal("branch_not_found", result.Error?.Code);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(31)]
    public async Task A_date_outside_the_booking_window_is_rejected(int daysFromToday)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();

        var result = await world.GetAvailability().ExecuteAsync(world.Branch.Id, Monday.AddDays(daysFromToday), ct);

        Assert.Equal("date_outside_booking_window", result.Error?.Code);
        Assert.Equal(ErrorType.Validation, result.Error?.Type);
    }
}
