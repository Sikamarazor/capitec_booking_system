using System.Globalization;
using BranchBooking.Domain.Entities;

namespace BranchBooking.Application.Common;

/// <summary>Conversions between UTC instants and a branch's local wall-clock time.</summary>
public static class BranchTime
{
    public static TimeZoneInfo ZoneOf(this Branch branch) => TimeZoneInfo.FindSystemTimeZoneById(branch.TimeZoneId);

    public static DateTimeOffset ToLocal(this Branch branch, DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, branch.ZoneOf());

    public static DateOnly LocalDateOf(this Branch branch, DateTimeOffset instant) =>
        DateOnly.FromDateTime(branch.ToLocal(instant).DateTime);

    /// <summary>The local start time as <c>HH:mm</c>, for display.</summary>
    public static string LocalTimeText(this Branch branch, DateTimeOffset instant) =>
        TimeOnly.FromDateTime(branch.ToLocal(instant).DateTime).ToString("HH:mm", CultureInfo.InvariantCulture);
}
