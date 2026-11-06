using System.Text.RegularExpressions;
using BranchBooking.Application.Abstractions;
using BranchBooking.Application.Common;
using BranchBooking.Domain.Enums;
using BranchBooking.UnitTests.Support;
using Xunit;

namespace BranchBooking.UnitTests.Bookings;

public partial class CreateBookingTests
{
    [GeneratedRegex("^BK-[0-9A-HJKMNP-TV-Z]{5}-[0-9A-HJKMNP-TV-Z]{5}$")]
    private static partial Regex ReferenceFormat();

    [Fact]
    public async Task Creates_a_confirmed_booking_in_position_one_and_returns_the_confirmation()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();

        var result = await world.CreateBooking().ExecuteAsync(world.ValidCommand(), ct);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Matches(ReferenceFormat(), dto.Reference);
        Assert.Equal("Confirmed", dto.Status);
        Assert.Equal(TestWorld.TuesdayEightAmUtc, dto.StartsAtUtc);
        Assert.Equal(TestWorld.TuesdayEightAmUtc.AddMinutes(30), dto.EndsAtUtc);
        Assert.Equal(TimeSpan.FromHours(2), dto.StartsAtLocal.Offset);
        Assert.Equal(8, dto.StartsAtLocal.Hour);
        Assert.Equal("Thandi Nkosi", dto.CustomerFullName);
        Assert.Equal("t*****@example.com", dto.CustomerEmail);
        Assert.Equal("*******567", dto.CustomerPhone);
        Assert.Equal(world.Type.Name, dto.AppointmentType.Name);
        Assert.Equal(world.Branch.Name, dto.Branch.Name);
        Assert.True(dto.CanCancel);
        Assert.True(dto.CanReschedule);

        var saved = Assert.Single(world.Store.Bookings);
        Assert.Equal(1, saved.SlotNumber);
        Assert.Equal(BookingStatus.Confirmed, saved.Status);
        Assert.Equal(dto.Reference, saved.Reference.Value);
        Assert.Equal("0821234567", saved.Customer.Phone);
    }

    [Fact]
    public async Task The_response_never_contains_the_full_email_or_phone()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();

        var result = await world.CreateBooking().ExecuteAsync(world.ValidCommand(), ct);

        Assert.DoesNotContain("thandi@example.com", result.Value.CustomerEmail, StringComparison.Ordinal);
        Assert.DoesNotContain("0821234567", result.Value.CustomerPhone, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Takes_the_next_free_position_when_the_first_is_taken()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(slotCapacity: 3);
        world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc, slotNumber: 1);

        await world.CreateBooking().ExecuteAsync(world.ValidCommand(), ct);

        Assert.Equal([1, 2], world.Store.Bookings.Select(b => b.SlotNumber).Order());
    }

    [Fact]
    public async Task Reuses_a_position_released_by_a_cancellation()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(slotCapacity: 1);
        var earlier = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        earlier.Cancel(world.Now.AddHours(1));

        var result = await world.CreateBooking().ExecuteAsync(world.ValidCommand(), ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task A_full_slot_is_a_conflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(slotCapacity: 1);
        world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);

        var result = await world.CreateBooking().ExecuteAsync(world.ValidCommand(), ct);

        Assert.Equal("slot_not_available", result.Error?.Code);
        Assert.Equal(ErrorType.Conflict, result.Error?.Type);
        Assert.Single(world.Store.Bookings);
        Assert.Equal(0, world.Store.SaveCalls);
    }

    [Fact]
    public async Task Retries_with_the_next_position_when_another_request_wins_the_race()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(slotCapacity: 2);
        world.Store.SaveFailures.Enqueue(new SlotAlreadyBookedException());
        world.Store.OnSaveFailure = () => world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc, slotNumber: 1);

        var result = await world.CreateBooking().ExecuteAsync(world.ValidCommand(), ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, world.Store.SaveCalls);
        Assert.Equal([1, 2], world.Store.Bookings.Select(b => b.SlotNumber).Order());
    }

    [Fact]
    public async Task Gives_up_with_a_conflict_after_capacity_plus_one_failed_attempts()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(slotCapacity: 2);
        for (var i = 0; i < 5; i++)
        {
            world.Store.SaveFailures.Enqueue(new SlotAlreadyBookedException());
        }

        var result = await world.CreateBooking().ExecuteAsync(world.ValidCommand(), ct);

        Assert.Equal("slot_not_available", result.Error?.Code);
        Assert.Equal(3, world.Store.SaveCalls);
        Assert.Empty(world.Store.Bookings);
    }

    [Fact]
    public async Task A_reference_collision_is_retried_with_a_new_reference()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        world.Store.SaveFailures.Enqueue(new DuplicateBookingReferenceException());

        var result = await world.CreateBooking().ExecuteAsync(world.ValidCommand(), ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, world.Store.SaveCalls);
        Assert.Single(world.Store.Bookings);
    }

    [Theory]
    [InlineData("", "thandi@example.com", "0821234567")]
    [InlineData("Thandi Nkosi", "not-an-email", "0821234567")]
    [InlineData("Thandi Nkosi", "thandi@example.com", "123")]
    public async Task Invalid_customer_details_are_rejected_before_anything_is_saved(string name, string email, string phone)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var command = world.ValidCommand() with { FullName = name, Email = email, Phone = phone };

        var result = await world.CreateBooking().ExecuteAsync(command, ct);

        Assert.Equal("invalid_customer_details", result.Error?.Code);
        Assert.Equal(ErrorType.Validation, result.Error?.Type);
        Assert.Equal(0, world.Store.SaveCalls);
    }

    [Fact]
    public async Task An_unknown_branch_is_not_found()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var command = world.ValidCommand() with { BranchId = Guid.CreateVersion7() };

        var result = await world.CreateBooking().ExecuteAsync(command, ct);

        Assert.Equal("branch_not_found", result.Error?.Code);
    }

    [Fact]
    public async Task An_inactive_branch_is_not_found()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        world.Branch.Deactivate();

        var result = await world.CreateBooking().ExecuteAsync(world.ValidCommand(), ct);

        Assert.Equal("branch_not_found", result.Error?.Code);
    }

    [Fact]
    public async Task An_unknown_or_inactive_appointment_type_is_not_found()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();

        var unknown = await world.CreateBooking().ExecuteAsync(world.ValidCommand() with { AppointmentTypeId = Guid.CreateVersion7() }, ct);
        world.Type.Deactivate();
        var inactive = await world.CreateBooking().ExecuteAsync(world.ValidCommand(), ct);

        Assert.Equal("appointment_type_not_found", unknown.Error?.Code);
        Assert.Equal("appointment_type_not_found", inactive.Error?.Code);
    }

    [Fact]
    public async Task A_time_that_is_not_a_slot_start_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();

        var result = await world.CreateBooking().ExecuteAsync(world.ValidCommand(TestWorld.TuesdayEightAmUtc.AddMinutes(10)), ct);

        Assert.Equal("slot_not_offered", result.Error?.Code);
        Assert.Equal(0, world.Store.SaveCalls);
    }

    [Fact]
    public async Task A_slot_inside_the_minimum_notice_is_rejected_as_a_policy_violation()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();

        var result = await world.CreateBooking().ExecuteAsync(
            world.ValidCommand(new DateTimeOffset(2026, 11, 2, 7, 30, 0, TimeSpan.Zero)), ct);

        Assert.Equal("slot_too_soon", result.Error?.Code);
        Assert.Equal(ErrorType.Policy, result.Error?.Type);
    }

    [Fact]
    public async Task The_start_time_may_be_given_with_any_offset()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var command = world.ValidCommand(TestWorld.TuesdayEightAmUtc.ToOffset(TimeSpan.FromHours(2)));

        var result = await world.CreateBooking().ExecuteAsync(command, ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(TestWorld.TuesdayEightAmUtc, Assert.Single(world.Store.Bookings).StartsAtUtc);
    }
}
