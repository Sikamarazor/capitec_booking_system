using BranchBooking.Application.Common;
using BranchBooking.Domain.Entities;
using BranchBooking.Domain.Enums;
using BranchBooking.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace BranchBooking.Application.Bookings;

/// <summary>The time-based business rules around booking and changing appointments.</summary>
public sealed class BookingPolicy(IOptions<BookingPolicyOptions> options, TimeProvider clock)
{
    private readonly BookingPolicyOptions _options = options.Value;

    public int MinimumNoticeMinutes => _options.MinimumNoticeMinutes;

    public int MaxAdvanceDays => _options.MaxAdvanceDays;

    public int ChangeCutoffHours => _options.ChangeCutoffHours;

    public int MaxReschedules => _options.MaxReschedules;

    /// <summary>Today's date on the branch's wall clock.</summary>
    public DateOnly TodayAt(Branch branch) => branch.LocalDateOf(clock.GetUtcNow());

    /// <summary>True from today (branch-local) up to <see cref="MaxAdvanceDays"/> days ahead, inclusive.</summary>
    public bool IsDateInBookingWindow(Branch branch, DateOnly localDate)
    {
        var today = TodayAt(branch);
        return localDate >= today && localDate <= today.AddDays(MaxAdvanceDays);
    }

    /// <summary>True when the slot starts at least the minimum notice from now.</summary>
    public bool IsSlotBookable(TimeSlot slot) =>
        slot.StartsAtUtc >= clock.GetUtcNow().AddMinutes(MinimumNoticeMinutes);

    /// <summary>True for a confirmed booking that starts at least the change cut-off from now.</summary>
    public bool CanChange(Booking booking) =>
        booking.Status == BookingStatus.Confirmed
        && booking.StartsAtUtc - clock.GetUtcNow() >= TimeSpan.FromHours(ChangeCutoffHours);

    public bool HasRescheduleAllowance(Booking booking) => booking.RescheduleCount < MaxReschedules;
}
