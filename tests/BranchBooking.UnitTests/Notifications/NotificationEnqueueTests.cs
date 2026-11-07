using BranchBooking.Application.Abstractions;
using BranchBooking.Domain.Enums;
using BranchBooking.UnitTests.Support;
using Xunit;

namespace BranchBooking.UnitTests.Notifications;

public class NotificationEnqueueTests
{
    private static readonly DateTimeOffset WednesdayEightAm = new(2026, 11, 4, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ThursdayEightAm = new(2026, 11, 5, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Creating_a_booking_queues_one_pending_confirmation_in_the_same_save()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();

        await world.CreateBooking().ExecuteAsync(world.ValidCommand(), ct);

        var booking = Assert.Single(world.Store.Bookings);
        var message = Assert.Single(world.Store.Messages);
        Assert.Equal(NotificationKind.Confirmation, message.Kind);
        Assert.Equal(NotificationStatus.Pending, message.Status);
        Assert.Equal(booking.Id, message.BookingId);
        Assert.Equal(booking.StartsAtUtc, message.StartsAtUtc);
        Assert.Equal(1, world.Store.SaveCalls);
    }

    [Fact]
    public async Task A_booking_that_cannot_be_created_queues_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(slotCapacity: 1);
        world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);

        await world.CreateBooking().ExecuteAsync(world.ValidCommand(), ct);

        Assert.Empty(world.Store.Messages);
        Assert.Empty(world.Store.StagedMessages);
    }

    [Fact]
    public async Task A_lost_race_leaves_exactly_one_confirmation()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(slotCapacity: 2);
        world.Store.SaveFailures.Enqueue(new SlotAlreadyBookedException());
        world.Store.OnSaveFailure = () => world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc, slotNumber: 1);

        var result = await world.CreateBooking().ExecuteAsync(world.ValidCommand(), ct);

        Assert.True(result.IsSuccess);
        Assert.Single(world.Store.Messages);
    }

    [Fact]
    public async Task Cancelling_queues_one_cancellation()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);

        await world.CancelBooking().ExecuteAsync(booking.Reference.Value, null, ct);

        var message = Assert.Single(world.Store.Messages);
        Assert.Equal(NotificationKind.Cancellation, message.Kind);
        Assert.Equal(booking.Id, message.BookingId);
    }

    [Fact]
    public async Task Cancelling_again_queues_nothing_more()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        var useCase = world.CancelBooking();

        await useCase.ExecuteAsync(booking.Reference.Value, null, ct);
        await useCase.ExecuteAsync(booking.Reference.Value, null, ct);

        Assert.Single(world.Store.Messages);
    }

    [Fact]
    public async Task A_rejected_cancellation_queues_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(world.Now.AddMinutes(90));

        await world.CancelBooking().ExecuteAsync(booking.Reference.Value, null, ct);

        Assert.Empty(world.Store.Messages);
    }

    [Fact]
    public async Task A_cancellation_retried_after_a_concurrent_change_leaves_one_message()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        var reference = booking.Reference;
        world.Store.SaveFailures.Enqueue(new ConcurrencyConflictException());
        world.Store.OnSaveFailure = () => world.ResetToFresh(reference, TestWorld.TuesdayEightAmUtc);

        var result = await world.CancelBooking().ExecuteAsync(reference.Value, null, ct);

        Assert.True(result.IsSuccess);
        Assert.Single(world.Store.Messages);
    }

    [Fact]
    public async Task Rescheduling_queues_a_reschedule_message_for_the_new_time()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);

        await world.RescheduleBooking().ExecuteAsync(booking.Reference.Value, WednesdayEightAm, ct);

        var message = Assert.Single(world.Store.Messages);
        Assert.Equal(NotificationKind.Reschedule, message.Kind);
        Assert.Equal(WednesdayEightAm, message.StartsAtUtc);
        Assert.Equal($"reschedule:{booking.Id:N}:1", message.DedupeKey);
    }

    [Fact]
    public async Task Two_reschedules_queue_two_distinct_messages()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        var useCase = world.RescheduleBooking();

        await useCase.ExecuteAsync(booking.Reference.Value, WednesdayEightAm, ct);
        await useCase.ExecuteAsync(booking.Reference.Value, ThursdayEightAm, ct);

        Assert.Equal(2, world.Store.Messages.Select(m => m.DedupeKey).Distinct().Count());
    }

    [Fact]
    public async Task A_rejected_reschedule_queues_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(slotCapacity: 1);
        world.AddConfirmedBooking(WednesdayEightAm, email: "other@example.com");
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);

        await world.RescheduleBooking().ExecuteAsync(booking.Reference.Value, WednesdayEightAm, ct);

        Assert.Empty(world.Store.Messages);
    }

    [Fact]
    public async Task A_reschedule_that_lost_a_race_leaves_exactly_one_message()
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
        Assert.Single(world.Store.Messages);
    }
}
