using System.Globalization;
using BranchBooking.Domain.Entities;

namespace BranchBooking.Application.Catalog;

public static class CatalogMapping
{
    public static AppointmentTypeDto ToDto(this AppointmentType type) =>
        new(type.Id, type.Code, type.Name, type.Description, type.WhatToBring);

    public static BranchDto ToDto(this Branch branch) =>
        new(
            branch.Id,
            branch.Code,
            branch.Name,
            branch.AddressLine,
            branch.City,
            branch.Province,
            branch.TimeZoneId,
            branch.SlotLengthMinutes,
            branch.OpeningHours
                .OrderBy(h => ((int)h.DayOfWeek + 6) % 7)   // Monday first
                .Select(h => new OpeningHoursDto(
                    h.DayOfWeek.ToString(),
                    h.OpensAt.ToString("HH:mm", CultureInfo.InvariantCulture),
                    h.ClosesAt.ToString("HH:mm", CultureInfo.InvariantCulture)))
                .ToList());
}
