using BranchBooking.Application.Common;
using BranchBooking.Domain.Entities;

namespace BranchBooking.Application.Bookings;

public sealed class BookingMapper(BookingPolicy policy)
{
    public BookingDto ToDto(Booking booking, Branch branch, AppointmentType type)
    {
        var canChange = policy.CanChange(booking);

        return new BookingDto(
            booking.Reference.Value,
            booking.Status.ToString(),
            new BookingAppointmentTypeDto(type.Id, type.Code, type.Name, type.WhatToBring),
            new BookingBranchDto(branch.Id, branch.Name, branch.AddressLine, branch.City, branch.Province, branch.TimeZoneId),
            booking.StartsAtUtc,
            booking.EndsAtUtc,
            branch.ToLocal(booking.StartsAtUtc),
            booking.Customer.FullName,
            ContactMasking.Email(booking.Customer.Email),
            ContactMasking.Phone(booking.Customer.Phone),
            booking.RescheduleCount,
            canChange,
            canChange && policy.HasRescheduleAllowance(booking),
            booking.CreatedAtUtc,
            booking.CancelledAtUtc);
    }

    /// <summary>For a booking loaded by the repository, which has its branch and type attached.</summary>
    public BookingDto ToDto(Booking booking) => ToDto(booking, booking.Branch, booking.AppointmentType);
}
