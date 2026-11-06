using BranchBooking.Application.Abstractions;
using BranchBooking.Application.Bookings;
using BranchBooking.Application.Common;
using BranchBooking.Domain.Enums;
using BranchBooking.UnitTests.Support;
using Xunit;

namespace BranchBooking.UnitTests.Bookings;

public class RescheduleBookingTests
{
    private static readonly DateOnly Tuesday = new(2026, 11, 3);
    private static readonly DateTimeOffset WednesdayEightAm = new(2026, 11, 4, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ThursdayEightAm = new(2026, 11, 5, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Moves_the_booking_to_the_new_time_and_counts_the_change()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);

        var result = await world.RescheduleBooking().ExecuteAsync(booking.Reference.Value, WednesdayEightAm, ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(WednesdayEightAm, result.Value.StartsAtUtc);
        Assert.Equal(WednesdayEightAm.AddMinutes(30), result.Value.EndsAtUtc);
        Assert.Equal(1, result.Value.RescheduleCount);
        Assert.Equal("Confirmed", result.Value.Status);
        Assert.Equal(WednesdayEightAm, booking.StartsAtUtc);
        Assert.Equal(1, booking.SlotNumber);
        Assert.Equal(1, world.Store.SaveCalls);
    }

    [Fact]
    public async Task Releases_the_old_place_and_takes_a_new_one()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(slotCapacity: 1);
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);

        await world.RescheduleBooking().ExecuteAsync(booking.Reference.Value, WednesdayEightAm, ct);

        var oldDay = await world.GetAvailability().ExecuteAsync(world.Branch.Id, Tuesday, ct);
        var newDay = await world.GetAvailability().ExecuteAsync(world.Branch.Id, Tuesday.AddDays(1), ct);
        Assert.Equal(1, oldDay.Value.Slots[0].Remaining);
        Assert.Equal(0, newDay.Value.Slots[0].Remaining);
    }

    [Fact]
    public async Task Takes_the_next_free_position_in_the_new_slot()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(slotCapacity: 2);
        world.AddConfirmedBooking(WednesdayEightAm, slotNumber: 1, email: "other@example.com");
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);

        var result = await world.RescheduleBooking().ExecuteAsync(booking.Reference.Value, WednesdayEightAm, ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, booking.SlotNumber);
    }

    [Fact]
    public async Task A_full_target_slot_is_a_conflict_and_the_booking_is_unchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(slotCapacity: 1);
        world.AddConfirmedBooking(WednesdayEightAm, email: "other@example.com");
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);

        var result = await world.RescheduleBooking().ExecuteAsync(booking.Reference.Value, WednesdayEightAm, ct);

        Assert.Equal("slot_not_available", result.Error?.Code);
        Assert.Equal(ErrorType.Conflict, result.Error?.Type);
        Assert.Equal(TestWorld.TuesdayEightAmUtc, booking.StartsAtUtc);
        Assert.Equal(0, booking.RescheduleCount);
        Assert.Equal(0, world.Store.SaveCalls);
    }

    [Fact]
    public async Task Rescheduling_to_the_current_time_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);

        var result = await world.RescheduleBooking().ExecuteAsync(booking.Reference.Value, TestWorld.TuesdayEightAmUtc, ct);

        Assert.Same(Errors.SameSlot, result.Error);
        Assert.Equal(0, world.Store.SaveCalls);
    }

    [Fact]
    public async Task A_cancelled_booking_cannot_be_rescheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        booking.Cancel(world.Now.AddHours(1));

        var result = await world.RescheduleBooking().ExecuteAsync(booking.Reference.Value, WednesdayEightAm, ct);

        Assert.Same(Errors.BookingNotActive, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error?.Type);
    }

    [Fact]
    public async Task Inside_the_cut_off_the_booking_cannot_be_rescheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(world.Now.AddMinutes(119));

        var result = await world.RescheduleBooking().ExecuteAsync(booking.Reference.Value, WednesdayEightAm, ct);

        Assert.Equal("change_window_closed", result.Error?.Code);
        Assert.Equal(ErrorType.Policy, result.Error?.Type);
    }

    [Fact]
    public async Task Stops_after_the_allowed_number_of_reschedules()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(policyOptions: new BookingPolicyOptions { MaxReschedules = 1 });
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        var useCase = world.RescheduleBooking();

        var first = await useCase.ExecuteAsync(booking.Reference.Value, WednesdayEightAm, ct);
        var second = await useCase.ExecuteAsync(booking.Reference.Value, ThursdayEightAm, ct);

        Assert.True(first.IsSuccess);
        Assert.Equal("reschedule_limit_reached", second.Error?.Code);
        Assert.Equal(ErrorType.Policy, second.Error?.Type);
        Assert.Equal(WednesdayEightAm, booking.StartsAtUtc);
    }

    [Fact]
    public async Task A_time_that_is_not_a_slot_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);

        var result = await world.RescheduleBooking().ExecuteAsync(booking.Reference.Value, WednesdayEightAm.AddMinutes(10), ct);

        Assert.Equal("slot_not_offered", result.Error?.Code);
    }

    [Fact]
    public async Task A_new_time_inside_the_minimum_notice_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);

        var result = await world.RescheduleBooking().ExecuteAsync(
            booking.Reference.Value, new DateTimeOffset(2026, 11, 2, 7, 30, 0, TimeSpan.Zero), ct);

        Assert.Equal("slot_too_soon", result.Error?.Code);
    }

    [Fact]
    public async Task A_new_date_outside_the_booking_window_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);

        var result = await world.RescheduleBooking().ExecuteAsync(
            booking.Reference.Value, new DateTimeOffset(2026, 12, 9, 6, 0, 0, TimeSpan.Zero), ct);

        Assert.Equal("date_outside_booking_window", result.Error?.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("hello")]
    [InlineData("BK-00000-00000")]
    public async Task An_unknown_or_malformed_reference_is_not_found(string? reference)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();

        var result = await world.RescheduleBooking().ExecuteAsync(reference, WednesdayEightAm, ct);

        Assert.Same(Errors.BookingNotFound, result.Error);
    }

    [Fact]
    public async Task Retries_with_the_next_position_when_another_request_wins_the_race()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(slotCapacity: 2);
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        var reference = booking.Reference;
        world.Store.SaveFailures.Enqueue(new SlotAlreadyBookedException());
        world.Store.OnSaveFailure = () =>
        {
            world.ResetToFresh(reference, TestWorld.TuesdayEightAmUtc);
            world.AddConfirmedBooking(WednesdayEightAm, slotNumber: 1, email: "rival@example.com");
        };

        var result = await world.RescheduleBooking().ExecuteAsync(reference.Value, WednesdayEightAm, ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, world.Store.SaveCalls);
        var saved = world.Store.Bookings.Single(b => b.Reference == reference);
        Assert.Equal(2, saved.SlotNumber);
        Assert.Equal(1, saved.RescheduleCount);
    }

    [Fact]
    public async Task Gives_up_with_a_conflict_when_the_target_keeps_filling()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(slotCapacity: 1);
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        var reference = booking.Reference;
        for (var i = 0; i < 5; i++)
        {
            world.Store.SaveFailures.Enqueue(new SlotAlreadyBookedException());
        }

        world.Store.OnSaveFailure = () => world.ResetToFresh(reference, TestWorld.TuesdayEightAmUtc);

        var result = await world.RescheduleBooking().ExecuteAsync(reference.Value, WednesdayEightAm, ct);

        Assert.Equal("slot_not_available", result.Error?.Code);
        Assert.Equal(2, world.Store.SaveCalls);
        Assert.Equal(TestWorld.TuesdayEightAmUtc, world.Store.Bookings.Single(b => b.Reference == reference).StartsAtUtc);
    }

    [Fact]
    public async Task Retries_after_a_concurrent_change_and_then_succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        var reference = booking.Reference;
        world.Store.SaveFailures.Enqueue(new ConcurrencyConflictException());
        world.Store.OnSaveFailure = () => world.ResetToFresh(reference, TestWorld.TuesdayEightAmUtc);

        var result = await world.RescheduleBooking().ExecuteAsync(reference.Value, WednesdayEightAm, ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, world.Store.SaveCalls);
    }

    [Fact]
    public async Task Gives_up_after_repeated_concurrent_changes()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        var reference = booking.Reference;
        for (var i = 0; i < 5; i++)
        {
            world.Store.SaveFailures.Enqueue(new ConcurrencyConflictException());
        }

        world.Store.OnSaveFailure = () => world.ResetToFresh(reference, TestWorld.TuesdayEightAmUtc);

        var result = await world.RescheduleBooking().ExecuteAsync(reference.Value, WednesdayEightAm, ct);

        Assert.Same(Errors.ConcurrentChange, result.Error);
        Assert.Equal(3, world.Store.SaveCalls);
    }
}
