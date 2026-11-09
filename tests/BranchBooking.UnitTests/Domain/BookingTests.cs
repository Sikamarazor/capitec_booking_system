using BranchBooking.Domain.Entities;
using BranchBooking.Domain.Enums;
using BranchBooking.Domain.Exceptions;
using BranchBooking.Domain.ValueObjects;
using BranchBooking.UnitTests.Support;
using Xunit;

namespace BranchBooking.UnitTests.Domain;

public class BookingTests
{
    private static readonly DateTimeOffset Now = DomainTestData.Now;

    [Fact]
    public void Create_makes_a_confirmed_booking_with_the_given_details()
    {
        var reference = BookingReference.Generate();
        var branchId = Guid.CreateVersion7();
        var typeId = Guid.CreateVersion7();
        var slot = DomainTestData.NewSlot(Now.AddDays(1));
        var customer = DomainTestData.NewCustomer();

        var booking = Booking.Create(reference, branchId, typeId, slot, 2, customer, Now);

        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(reference, booking.Reference);
        Assert.Equal(branchId, booking.BranchId);
        Assert.Equal(typeId, booking.AppointmentTypeId);
        Assert.Equal(slot, booking.Slot);
        Assert.Equal(2, booking.SlotNumber);
        Assert.Same(customer, booking.Customer);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(0, booking.RescheduleCount);
        Assert.Equal(Now, booking.CreatedAtUtc);
        Assert.Equal(Now, booking.UpdatedAtUtc);
        Assert.Null(booking.CancelledAtUtc);
    }

    [Fact]
    public void Create_rejects_a_slot_in_the_past_or_starting_now()
    {
        Assert.Throws<DomainException>(() => DomainTestData.NewBooking(Now));
        Assert.Throws<DomainException>(() => DomainTestData.NewBooking(Now.AddMinutes(-30)));
    }

    [Fact]
    public void Create_rejects_a_slot_number_below_one()
    {
        Assert.Throws<DomainException>(() => DomainTestData.NewBooking(slotNumber: 0));
    }

    [Fact]
    public void Create_rejects_a_default_reference()
    {
        Assert.Throws<DomainException>(() => Booking.Create(
            default, Guid.CreateVersion7(), Guid.CreateVersion7(),
            DomainTestData.NewSlot(Now.AddDays(1)), 1, DomainTestData.NewCustomer(), Now));
    }

    [Fact]
    public void Create_rejects_empty_branch_or_type_ids()
    {
        var slot = DomainTestData.NewSlot(Now.AddDays(1));

        Assert.Throws<DomainException>(() => Booking.Create(
            BookingReference.Generate(), Guid.Empty, Guid.CreateVersion7(), slot, 1, DomainTestData.NewCustomer(), Now));
        Assert.Throws<DomainException>(() => Booking.Create(
            BookingReference.Generate(), Guid.CreateVersion7(), Guid.Empty, slot, 1, DomainTestData.NewCustomer(), Now));
    }

    [Fact]
    public void Cancel_marks_the_booking_cancelled_and_records_when_and_why()
    {
        var booking = DomainTestData.NewBooking();
        var later = Now.AddHours(1);

        booking.Cancel(later, "  Plans changed  ");

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(later, booking.CancelledAtUtc);
        Assert.Equal("Plans changed", booking.CancellationReason);
        Assert.Equal(later, booking.UpdatedAtUtc);
    }

    [Fact]
    public void Cancel_without_a_reason_stores_no_reason()
    {
        var booking = DomainTestData.NewBooking();

        booking.Cancel(Now.AddHours(1), "   ");

        Assert.Null(booking.CancellationReason);
    }

    [Fact]
    public void Cancel_twice_is_rejected()
    {
        var booking = DomainTestData.NewBooking();
        booking.Cancel(Now.AddHours(1));

        Assert.Throws<DomainException>(() => booking.Cancel(Now.AddHours(2)));
    }

    [Fact]
    public void Cancel_after_the_appointment_has_started_is_rejected()
    {
        var booking = DomainTestData.NewBooking(Now.AddDays(1));

        Assert.Throws<DomainException>(() => booking.Cancel(Now.AddDays(1)));
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
    }

    [Fact]
    public void Cancel_rejects_a_reason_over_the_limit()
    {
        var booking = DomainTestData.NewBooking();

        Assert.Throws<DomainException>(() =>
            booking.Cancel(Now.AddHours(1), new string('x', Booking.CancellationReasonMaxLength + 1)));
    }

    [Fact]
    public void Reschedule_moves_the_booking_and_counts_the_change()
    {
        var booking = DomainTestData.NewBooking(Now.AddDays(1), slotNumber: 1);
        var newSlot = DomainTestData.NewSlot(Now.AddDays(2));
        var later = Now.AddHours(1);

        booking.Reschedule(newSlot, 2, later);

        Assert.Equal(newSlot, booking.Slot);
        Assert.Equal(2, booking.SlotNumber);
        Assert.Equal(1, booking.RescheduleCount);
        Assert.Equal(later, booking.UpdatedAtUtc);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
    }

    [Fact]
    public void Reschedule_to_the_same_time_and_position_is_rejected()
    {
        var booking = DomainTestData.NewBooking(Now.AddDays(1), slotNumber: 1);

        Assert.Throws<DomainException>(() =>
            booking.Reschedule(DomainTestData.NewSlot(Now.AddDays(1)), 1, Now.AddHours(1)));
    }

    [Fact]
    public void Reschedule_to_the_same_time_with_a_different_position_is_allowed()
    {
        var booking = DomainTestData.NewBooking(Now.AddDays(1), slotNumber: 1);

        booking.Reschedule(DomainTestData.NewSlot(Now.AddDays(1)), 2, Now.AddHours(1));

        Assert.Equal(2, booking.SlotNumber);
    }

    [Fact]
    public void Reschedule_to_a_past_time_is_rejected()
    {
        var booking = DomainTestData.NewBooking();

        Assert.Throws<DomainException>(() =>
            booking.Reschedule(DomainTestData.NewSlot(Now.AddMinutes(-30)), 1, Now));
    }

    [Fact]
    public void Reschedule_a_cancelled_booking_is_rejected()
    {
        var booking = DomainTestData.NewBooking();
        booking.Cancel(Now.AddHours(1));

        Assert.Throws<DomainException>(() =>
            booking.Reschedule(DomainTestData.NewSlot(Now.AddDays(3)), 1, Now.AddHours(2)));
    }

    [Fact]
    public void Reschedule_after_the_appointment_has_started_is_rejected()
    {
        var booking = DomainTestData.NewBooking(Now.AddDays(1));

        Assert.Throws<DomainException>(() =>
            booking.Reschedule(DomainTestData.NewSlot(Now.AddDays(3)), 1, Now.AddDays(1).AddMinutes(5)));
    }
}
