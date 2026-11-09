using BranchBooking.Application.Abstractions;
using BranchBooking.Application.Bookings;
using BranchBooking.Application.Common;

namespace BranchBooking.Application.Availability;

/// <summary>
/// The slots a branch offers on a local date. Slots that start sooner than the minimum notice are left out;
/// full slots are kept and marked unavailable so the wizard can show them disabled.
/// </summary>
public sealed class GetAvailability(IBranchRepository branches, IBookingRepository bookings, BookingPolicy policy)
{
    public async Task<Result<AvailabilityDto>> ExecuteAsync(Guid branchId, DateOnly date, CancellationToken cancellationToken)
    {
        var branch = await branches.GetByIdAsync(branchId, cancellationToken);
        if (branch is null || !branch.IsActive)
        {
            return Result<AvailabilityDto>.Failure(Errors.BranchNotFound);
        }

        if (!policy.IsDateInBookingWindow(branch, date))
        {
            return Result<AvailabilityDto>.Failure(Errors.DateOutsideBookingWindow(policy.MaxAdvanceDays));
        }

        var slots = branch.GetSlotsFor(date);
        if (slots.Count == 0)
        {
            return Result<AvailabilityDto>.Success(new AvailabilityDto(branch.Id, date, branch.TimeZoneId, []));
        }

        var booked = await bookings.GetConfirmedPositionsAsync(
            branch.Id, slots[0].StartsAtUtc, slots[^1].EndsAtUtc, cancellationToken);
        var takenByStart = booked.GroupBy(p => p.StartsAtUtc).ToDictionary(g => g.Key, g => g.Count());

        var offered = slots
            .Where(policy.IsSlotBookable)
            .Select(slot =>
            {
                var remaining = Math.Max(0, branch.SlotCapacity - takenByStart.GetValueOrDefault(slot.StartsAtUtc));
                return new SlotDto(slot.StartsAtUtc, slot.EndsAtUtc, branch.LocalTimeText(slot.StartsAtUtc), remaining, remaining > 0);
            })
            .ToList();

        return Result<AvailabilityDto>.Success(new AvailabilityDto(branch.Id, date, branch.TimeZoneId, offered));
    }
}
