using BranchBooking.Application.Abstractions;
using BranchBooking.Application.Bookings;
using Xunit;

namespace BranchBooking.UnitTests.Bookings;

public class SlotPositionsTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_empty_slot_gives_position_one()
    {
        Assert.Equal(1, SlotPositions.FirstFree([], Start, 3));
    }

    [Fact]
    public void The_lowest_free_position_is_chosen_even_when_a_gap_exists()
    {
        BookedPosition[] taken = [new(Start, 1), new(Start, 3)];

        Assert.Equal(2, SlotPositions.FirstFree(taken, Start, 3));
    }

    [Fact]
    public void A_full_slot_gives_null()
    {
        BookedPosition[] taken = [new(Start, 1), new(Start, 2)];

        Assert.Null(SlotPositions.FirstFree(taken, Start, 2));
    }

    [Fact]
    public void Positions_held_at_other_times_are_ignored()
    {
        BookedPosition[] taken = [new(Start.AddMinutes(30), 1)];

        Assert.Equal(1, SlotPositions.FirstFree(taken, Start, 2));
    }
}
