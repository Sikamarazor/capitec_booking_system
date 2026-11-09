using BranchBooking.Domain.Exceptions;
using BranchBooking.Domain.ValueObjects;
using Xunit;

namespace BranchBooking.UnitTests.Domain;

public class TimeSlotTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Creates_a_slot_and_reports_its_duration()
    {
        var slot = new TimeSlot(Start, Start.AddMinutes(30));

        Assert.Equal(TimeSpan.FromMinutes(30), slot.Duration);
    }

    [Fact]
    public void Rejects_times_that_are_not_utc()
    {
        var local = new DateTimeOffset(2026, 11, 3, 8, 0, 0, TimeSpan.FromHours(2));

        Assert.Throws<DomainException>(() => new TimeSlot(local, local.AddMinutes(30)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void Rejects_an_end_that_is_not_after_the_start(int minutes)
    {
        Assert.Throws<DomainException>(() => new TimeSlot(Start, Start.AddMinutes(minutes)));
    }

    [Fact]
    public void Overlapping_slots_overlap()
    {
        var a = new TimeSlot(Start, Start.AddMinutes(30));
        var b = new TimeSlot(Start.AddMinutes(15), Start.AddMinutes(45));

        Assert.True(a.Overlaps(b));
        Assert.True(b.Overlaps(a));
    }

    [Fact]
    public void Adjacent_slots_do_not_overlap()
    {
        var a = new TimeSlot(Start, Start.AddMinutes(30));
        var b = new TimeSlot(Start.AddMinutes(30), Start.AddMinutes(60));

        Assert.False(a.Overlaps(b));
        Assert.False(b.Overlaps(a));
    }
}
