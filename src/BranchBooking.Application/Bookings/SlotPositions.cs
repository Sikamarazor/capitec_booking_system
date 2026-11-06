using BranchBooking.Application.Abstractions;

namespace BranchBooking.Application.Bookings;

public static class SlotPositions
{
    /// <summary>The lowest position from 1 to <paramref name="capacity"/> not held at <paramref name="startsAtUtc"/>, or <c>null</c> when the slot is full.</summary>
    public static int? FirstFree(IEnumerable<BookedPosition> taken, DateTimeOffset startsAtUtc, int capacity)
    {
        var used = taken.Where(p => p.StartsAtUtc == startsAtUtc).Select(p => p.SlotNumber).ToHashSet();

        for (var position = 1; position <= capacity; position++)
        {
            if (!used.Contains(position))
            {
                return position;
            }
        }

        return null;
    }
}
