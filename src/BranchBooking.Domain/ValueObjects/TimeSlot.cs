using BranchBooking.Domain.Exceptions;

namespace BranchBooking.Domain.ValueObjects;

/// <summary>A half-open interval [StartsAtUtc, EndsAtUtc) in UTC.</summary>
public readonly record struct TimeSlot
{
    public TimeSlot(DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc)
    {
        if (startsAtUtc.Offset != TimeSpan.Zero || endsAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainException("Slot times must be expressed in UTC.");
        }

        if (endsAtUtc <= startsAtUtc)
        {
            throw new DomainException("A slot must end after it starts.");
        }

        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
    }

    public DateTimeOffset StartsAtUtc { get; }

    public DateTimeOffset EndsAtUtc { get; }

    public TimeSpan Duration => EndsAtUtc - StartsAtUtc;

    public bool Overlaps(TimeSlot other) => StartsAtUtc < other.EndsAtUtc && other.StartsAtUtc < EndsAtUtc;
}
