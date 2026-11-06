using BranchBooking.Application.Common;
using BranchBooking.Domain.Entities;
using BranchBooking.Domain.ValueObjects;

namespace BranchBooking.Application.Bookings;

/// <summary>Checks that a requested start is a real, bookable slot at the branch and returns it.</summary>
public sealed class SlotResolver(BookingPolicy policy)
{
    public Result<TimeSlot> Resolve(Branch branch, DateTimeOffset startsAt)
    {
        var localDate = branch.LocalDateOf(startsAt);
        if (!policy.IsDateInBookingWindow(branch, localDate))
        {
            return Result<TimeSlot>.Failure(Errors.DateOutsideBookingWindow(policy.MaxAdvanceDays));
        }

        TimeSlot? slot = branch.GetSlotsFor(localDate)
            .Where(s => s.StartsAtUtc == startsAt)
            .Select(s => (TimeSlot?)s)
            .FirstOrDefault();

        if (slot is null)
        {
            return Result<TimeSlot>.Failure(Errors.SlotNotOffered);
        }

        if (!policy.IsSlotBookable(slot.Value))
        {
            return Result<TimeSlot>.Failure(Errors.SlotTooSoon(policy.MinimumNoticeMinutes));
        }

        return Result<TimeSlot>.Success(slot.Value);
    }
}
