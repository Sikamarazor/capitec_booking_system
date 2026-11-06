namespace BranchBooking.Application.Availability;

/// <param name="LocalStart">Start time on the branch's wall clock, <c>HH:mm</c>.</param>
/// <param name="Remaining">Places still free in this slot.</param>
public sealed record SlotDto(DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, string LocalStart, int Remaining, bool IsAvailable);

public sealed record AvailabilityDto(Guid BranchId, DateOnly Date, string TimeZoneId, IReadOnlyList<SlotDto> Slots);
