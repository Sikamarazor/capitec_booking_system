namespace BranchBooking.Application.Abstractions;

/// <summary>One occupied position: a confirmed booking's start time and slot number.</summary>
public sealed record BookedPosition(DateTimeOffset StartsAtUtc, int SlotNumber);
