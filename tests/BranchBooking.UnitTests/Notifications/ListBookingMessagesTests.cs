using BranchBooking.Application.Common;
using BranchBooking.Domain.Entities;
using BranchBooking.UnitTests.Support;
using Xunit;

namespace BranchBooking.UnitTests.Notifications;

public class ListBookingMessagesTests
{
    [Fact]
    public async Task A_booking_with_no_messages_gives_an_empty_list()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);

        var result = await world.ListBookingMessages().ExecuteAsync(booking.Reference.Value, ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task A_pending_message_shows_its_status_but_no_content()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        world.Store.Messages.Add(NotificationMessage.ForConfirmation(booking, world.Now));

        var result = await world.ListBookingMessages().ExecuteAsync(booking.Reference.Value, ct);

        var dto = Assert.Single(result.Value);
        Assert.Equal("Confirmation", dto.Kind);
        Assert.Equal("Pending", dto.Status);
        Assert.Null(dto.Channel);
        Assert.Null(dto.SentAtUtc);
        Assert.Null(dto.Subject);
        Assert.Null(dto.Body);
    }

    [Fact]
    public async Task A_sent_simulated_message_shows_its_subject_and_body()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        var message = NotificationMessage.ForConfirmation(booking, world.Now);
        message.MarkSent("Simulated", "sim-1", "Hello subject", "Hello body", world.Now.AddMinutes(1));
        world.Store.Messages.Add(message);

        var result = await world.ListBookingMessages().ExecuteAsync(booking.Reference.Value, ct);

        var dto = Assert.Single(result.Value);
        Assert.Equal("Sent", dto.Status);
        Assert.Equal("Simulated", dto.Channel);
        Assert.Equal(world.Now.AddMinutes(1), dto.SentAtUtc);
        Assert.Equal("Hello subject", dto.Subject);
        Assert.Equal("Hello body", dto.Body);
    }

    [Fact]
    public async Task Messages_come_back_oldest_first_and_only_for_this_booking()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        var other = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc.AddMinutes(30), email: "other@example.com");
        world.Store.Messages.Add(NotificationMessage.ForCancellation(booking, world.Now.AddMinutes(5)));
        world.Store.Messages.Add(NotificationMessage.ForConfirmation(booking, world.Now));
        world.Store.Messages.Add(NotificationMessage.ForConfirmation(other, world.Now));

        var result = await world.ListBookingMessages().ExecuteAsync(booking.Reference.Value, ct);

        Assert.Equal(["Confirmation", "Cancellation"], result.Value.Select(m => m.Kind));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("hello")]
    [InlineData("BK-00000-00000")]
    public async Task An_unknown_or_malformed_reference_is_not_found(string? reference)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();

        var result = await world.ListBookingMessages().ExecuteAsync(reference, ct);

        Assert.Same(Errors.BookingNotFound, result.Error);
    }
}
