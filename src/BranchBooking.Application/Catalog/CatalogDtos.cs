namespace BranchBooking.Application.Catalog;

public sealed record AppointmentTypeDto(Guid Id, string Code, string Name, string Description, string WhatToBring);

/// <param name="Day">English weekday name, for example <c>Monday</c>.</param>
/// <param name="OpensAt">Local time, <c>HH:mm</c>.</param>
/// <param name="ClosesAt">Local time, <c>HH:mm</c>.</param>
public sealed record OpeningHoursDto(string Day, string OpensAt, string ClosesAt);

public sealed record BranchDto(
    Guid Id,
    string Code,
    string Name,
    string AddressLine,
    string City,
    string Province,
    string TimeZoneId,
    int SlotLengthMinutes,
    IReadOnlyList<OpeningHoursDto> OpeningHours);
