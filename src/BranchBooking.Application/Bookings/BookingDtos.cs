namespace BranchBooking.Application.Bookings;

public sealed record BookingAppointmentTypeDto(Guid Id, string Code, string Name, string WhatToBring);

public sealed record BookingBranchDto(Guid Id, string Name, string AddressLine, string City, string Province, string TimeZoneId);

/// <param name="Reference">The customer's only credential for this booking.</param>
/// <param name="Status"><c>Confirmed</c> or <c>Cancelled</c>.</param>
/// <param name="StartsAtLocal">The start on the branch's wall clock, with that clock's offset.</param>
/// <param name="CustomerEmail">Masked, for example <c>t*****@example.com</c>.</param>
/// <param name="CustomerPhone">Masked, last three digits only.</param>
/// <param name="CanCancel">Cancellation is allowed right now under the booking policy.</param>
/// <param name="CanReschedule">Rescheduling is allowed right now under the booking policy.</param>
public sealed record BookingDto(
    string Reference,
    string Status,
    BookingAppointmentTypeDto AppointmentType,
    BookingBranchDto Branch,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset StartsAtLocal,
    string CustomerFullName,
    string CustomerEmail,
    string CustomerPhone,
    int RescheduleCount,
    bool CanCancel,
    bool CanReschedule,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CancelledAtUtc);

/// <summary>What the customer typed when booking; validated by the use case.</summary>
public sealed record CreateBookingCommand(
    Guid BranchId, Guid AppointmentTypeId, DateTimeOffset StartsAtUtc, string FullName, string Email, string Phone);
