using BranchBooking.Application.Bookings;
using BranchBooking.Application.Common;
using BranchBooking.UnitTests.Support;
using Xunit;

namespace BranchBooking.UnitTests.Bookings;

public class GetBookingTests
{
    [Fact]
    public async Task Returns_the_booking_with_masked_contact_details_and_change_flags()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);

        var result = await world.GetBooking().ExecuteAsync(booking.Reference.Value, ct);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Equal(booking.Reference.Value, dto.Reference);
        Assert.Equal("Confirmed", dto.Status);
        Assert.Equal(world.Branch.Name, dto.Branch.Name);
        Assert.Equal(world.Type.Code, dto.AppointmentType.Code);
        Assert.Equal(TestWorld.TuesdayEightAmUtc, dto.StartsAtUtc);
        Assert.Equal("t*****@example.com", dto.CustomerEmail);
        Assert.Equal("*******567", dto.CustomerPhone);
        Assert.True(dto.CanCancel);
        Assert.True(dto.CanReschedule);
        Assert.Null(dto.CancelledAtUtc);
    }

    [Fact]
    public async Task Accepts_any_casing_and_spacing_of_the_reference()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        var typed = "  " + booking.Reference.Value.ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal) + " ";

        var result = await world.GetBooking().ExecuteAsync(typed, ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(booking.Reference.Value, result.Value.Reference);
    }

    [Fact]
    public async Task An_unknown_reference_is_not_found()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);

        var result = await world.GetBooking().ExecuteAsync("BK-00000-00000", ct);

        Assert.False(result.IsSuccess);
        Assert.Equal("booking_not_found", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("BK-123")]
    [InlineData("XX-00000-00000")]
    public async Task A_malformed_reference_gives_exactly_the_same_error_as_an_unknown_one(string? reference)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();

        var malformed = await world.GetBooking().ExecuteAsync(reference, ct);
        var unknown = await world.GetBooking().ExecuteAsync("BK-00000-00000", ct);

        Assert.Same(unknown.Error, malformed.Error);
    }

    [Fact]
    public async Task A_cancelled_booking_is_shown_as_cancelled_and_cannot_be_changed()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        booking.Cancel(world.Now.AddHours(1));

        var result = await world.GetBooking().ExecuteAsync(booking.Reference.Value, ct);

        Assert.Equal("Cancelled", result.Value.Status);
        Assert.Equal(world.Now.AddHours(1), result.Value.CancelledAtUtc);
        Assert.False(result.Value.CanCancel);
        Assert.False(result.Value.CanReschedule);
    }

    [Fact]
    public async Task A_booking_inside_the_change_cut_off_cannot_be_changed()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(world.Now.AddMinutes(90));

        var result = await world.GetBooking().ExecuteAsync(booking.Reference.Value, ct);

        Assert.Equal("Confirmed", result.Value.Status);
        Assert.False(result.Value.CanCancel);
        Assert.False(result.Value.CanReschedule);
    }

    [Fact]
    public async Task A_booking_that_used_up_its_reschedules_can_still_be_cancelled_but_not_rescheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(policyOptions: new BookingPolicyOptions { MaxReschedules = 0 });
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);

        var result = await world.GetBooking().ExecuteAsync(booking.Reference.Value, ct);

        Assert.True(result.Value.CanCancel);
        Assert.False(result.Value.CanReschedule);
    }
}
