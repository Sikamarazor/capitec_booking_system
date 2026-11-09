namespace BranchBooking.Application.Bookings;

/// <summary>Business-policy settings (<c>BookingPolicy</c> section). Defaults are planner choices awaiting business confirmation.</summary>
public sealed class BookingPolicyOptions
{
    public const string SectionName = "BookingPolicy";

    /// <summary>A new booking or reschedule must start at least this many minutes from now.</summary>
    public int MinimumNoticeMinutes { get; set; } = 60;

    /// <summary>Customers can book up to this many days after today (branch-local date).</summary>
    public int MaxAdvanceDays { get; set; } = 30;

    /// <summary>Cancel and reschedule are allowed until this many hours before the start.</summary>
    public int ChangeCutoffHours { get; set; } = 2;

    /// <summary>How many times one booking may be rescheduled.</summary>
    public int MaxReschedules { get; set; } = 3;

    public bool IsValid() =>
        MinimumNoticeMinutes is >= 1 and <= 10_080
        && MaxAdvanceDays is >= 1 and <= 365
        && ChangeCutoffHours is >= 0 and <= 168
        && MaxReschedules is >= 0 and <= 20;
}
