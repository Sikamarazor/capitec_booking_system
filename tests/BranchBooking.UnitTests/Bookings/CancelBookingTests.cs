using BranchBooking.Application.Abstractions;
using BranchBooking.Application.Common;
using BranchBooking.Domain.Enums;
using BranchBooking.UnitTests.Support;
using Xunit;

namespace BranchBooking.UnitTests.Bookings;

public class CancelBookingTests
{
    private static readonly DateOnly Tuesday = new(2026, 11, 3);

    [Fact]
    public async Task Cancels_a_confirmed_booking_and_records_the_time_and_reason()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);

        var result = await world.CancelBooking().ExecuteAsync(booking.Reference.Value, "  Plans changed ", ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", result.Value.Status);
        Assert.Equal(world.Now, result.Value.CancelledAtUtc);
        Assert.False(result.Value.CanCancel);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal("Plans changed", booking.CancellationReason);
        Assert.Equal(1, world.Store.SaveCalls);
    }

    [Fact]
    public async Task The_reason_is_optional()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);

        var result = await world.CancelBooking().ExecuteAsync(booking.Reference.Value, null, ct);

        Assert.True(result.IsSuccess);
        Assert.Null(booking.CancellationReason);
    }

    [Fact]
    public async Task Cancelling_again_succeeds_without_saving()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        var useCase = world.CancelBooking();

        await useCase.ExecuteAsync(booking.Reference.Value, null, ct);
        var again = await useCase.ExecuteAsync(booking.Reference.Value, null, ct);

        Assert.True(again.IsSuccess);
        Assert.Equal("Cancelled", again.Value.Status);
        Assert.Equal(1, world.Store.SaveCalls);
    }

    [Fact]
    public async Task The_place_is_released_for_other_customers()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(slotCapacity: 1);
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        var before = await world.GetAvailability().ExecuteAsync(world.Branch.Id, Tuesday, ct);

        await world.CancelBooking().ExecuteAsync(booking.Reference.Value, null, ct);
        var after = await world.GetAvailability().ExecuteAsync(world.Branch.Id, Tuesday, ct);

        Assert.Equal(0, before.Value.Slots[0].Remaining);
        Assert.Equal(1, after.Value.Slots[0].Remaining);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("hello")]
    [InlineData("BK-00000-00000")]
    public async Task An_unknown_or_malformed_reference_is_not_found(string? reference)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();

        var result = await world.CancelBooking().ExecuteAsync(reference, null, ct);

        Assert.Same(Errors.BookingNotFound, result.Error);
        Assert.Equal(0, world.Store.SaveCalls);
    }

    [Fact]
    public async Task Inside_the_cut_off_the_booking_cannot_be_cancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(world.Now.AddMinutes(119));

        var result = await world.CancelBooking().ExecuteAsync(booking.Reference.Value, null, ct);

        Assert.Equal("change_window_closed", result.Error?.Code);
        Assert.Equal(ErrorType.Policy, result.Error?.Type);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(0, world.Store.SaveCalls);
    }

    [Fact]
    public async Task Exactly_at_the_cut_off_the_booking_can_still_be_cancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(world.Now.AddMinutes(120));

        var result = await world.CancelBooking().ExecuteAsync(booking.Reference.Value, null, ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task A_reason_over_the_limit_is_rejected_before_anything_changes()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);

        var result = await world.CancelBooking().ExecuteAsync(booking.Reference.Value, new string('x', 501), ct);

        Assert.Equal("invalid_cancellation_reason", result.Error?.Code);
        Assert.Equal(ErrorType.Validation, result.Error?.Type);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
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

        var result = await world.CancelBooking().ExecuteAsync(reference.Value, null, ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, world.Store.SaveCalls);
        Assert.Equal("Cancelled", result.Value.Status);
    }

    [Fact]
    public async Task When_the_concurrent_change_was_a_cancellation_the_result_is_success_without_another_save()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        var reference = booking.Reference;
        world.Store.SaveFailures.Enqueue(new ConcurrencyConflictException());
        world.Store.OnSaveFailure = () => world.ResetToFresh(reference, TestWorld.TuesdayEightAmUtc).Cancel(world.Now);

        var result = await world.CancelBooking().ExecuteAsync(reference.Value, null, ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", result.Value.Status);
        Assert.Equal(1, world.Store.SaveCalls);
    }

    [Fact]
    public async Task Gives_up_with_a_conflict_after_three_concurrent_changes()
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

        var result = await world.CancelBooking().ExecuteAsync(reference.Value, null, ct);

        Assert.Same(Errors.ConcurrentChange, result.Error);
        Assert.Equal(3, world.Store.SaveCalls);
    }
}
