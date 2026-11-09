using BranchBooking.Domain.Entities;
using BranchBooking.Domain.Exceptions;
using BranchBooking.UnitTests.Support;
using Xunit;

namespace BranchBooking.UnitTests.Domain;

public class BranchTests
{
    private static readonly DateOnly Monday = new(2026, 11, 2);
    private static readonly DateOnly Saturday = new(2026, 11, 7);
    private static readonly DateOnly Sunday = new(2026, 11, 8);

    [Fact]
    public void Monday_has_eighteen_thirty_minute_slots_in_utc()
    {
        var slots = DomainTestData.NewBranch(slotLengthMinutes: 30).GetSlotsFor(Monday);

        Assert.Equal(18, slots.Count);
        Assert.All(slots, s => Assert.Equal(TimeSpan.FromMinutes(30), s.Duration));
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 6, 0, 0, TimeSpan.Zero), slots[0].StartsAtUtc);   // 08:00 SAST
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 15, 0, 0, TimeSpan.Zero), slots[^1].EndsAtUtc);   // 17:00 SAST
    }

    [Fact]
    public void Saturday_has_ten_slots()
    {
        Assert.Equal(10, DomainTestData.NewBranch().GetSlotsFor(Saturday).Count);
    }

    [Fact]
    public void Sunday_has_no_slots_because_the_branch_is_closed()
    {
        Assert.Empty(DomainTestData.NewBranch().GetSlotsFor(Sunday));
    }

    [Fact]
    public void Slots_never_run_past_closing_time()
    {
        // Open 08:00 to 17:00 is 540 minutes. Thirteen 40-minute slots fit (520 minutes); a fourteenth would end at 17:20.
        var slots = DomainTestData.NewBranch(slotLengthMinutes: 40).GetSlotsFor(Monday);

        Assert.Equal(13, slots.Count);
        Assert.True(slots[^1].EndsAtUtc <= new DateTimeOffset(2026, 11, 2, 15, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Slots_are_contiguous_and_do_not_overlap()
    {
        var slots = DomainTestData.NewBranch(slotLengthMinutes: 45).GetSlotsFor(Monday);

        Assert.Equal(12, slots.Count);
        for (var i = 1; i < slots.Count; i++)
        {
            Assert.Equal(slots[i - 1].EndsAtUtc, slots[i].StartsAtUtc);
        }
    }

    [Fact]
    public void Create_sets_defaults()
    {
        var branch = DomainTestData.NewBranch(slotLengthMinutes: 30, slotCapacity: 3);

        Assert.Equal("TEST-001", branch.Code);
        Assert.Equal(30, branch.SlotLengthMinutes);
        Assert.Equal(3, branch.SlotCapacity);
        Assert.True(branch.IsActive);
        Assert.Equal(6, branch.OpeningHours.Count);
    }

    [Theory]
    [InlineData("", "Name", 30, 1, "Africa/Johannesburg")]
    [InlineData("ab", "Name", 30, 1, "Africa/Johannesburg")]
    [InlineData("test-001", "Name", 30, 1, "Africa/Johannesburg")]
    [InlineData("TEST-001", " ", 30, 1, "Africa/Johannesburg")]
    [InlineData("TEST-001", "Name", 4, 1, "Africa/Johannesburg")]
    [InlineData("TEST-001", "Name", 241, 1, "Africa/Johannesburg")]
    [InlineData("TEST-001", "Name", 30, 0, "Africa/Johannesburg")]
    [InlineData("TEST-001", "Name", 30, 51, "Africa/Johannesburg")]
    [InlineData("TEST-001", "Name", 30, 1, "Mars/Olympus_Mons")]
    [InlineData("TEST-001", "Name", 30, 1, "")]
    public void Create_rejects_invalid_values(string code, string name, int slotLength, int capacity, string zone)
    {
        Assert.Throws<DomainException>(() =>
            Branch.Create(Guid.CreateVersion7(), code, name, "1 Street", "City", "Province", zone, slotLength, capacity));
    }

    [Fact]
    public void Create_rejects_an_empty_id()
    {
        Assert.Throws<DomainException>(() =>
            Branch.Create(Guid.Empty, "TEST-001", "Name", "1 Street", "City", "Province", "Africa/Johannesburg", 30, 1));
    }

    [Fact]
    public void AddOpeningHours_rejects_a_duplicate_day()
    {
        var branch = DomainTestData.NewBranch();

        Assert.Throws<DomainException>(() =>
            branch.AddOpeningHours(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0)));
    }

    [Fact]
    public void AddOpeningHours_rejects_closing_before_opening()
    {
        var branch = DomainTestData.NewBranch();

        Assert.Throws<DomainException>(() =>
            branch.AddOpeningHours(DayOfWeek.Sunday, new TimeOnly(12, 0), new TimeOnly(9, 0)));
    }

    [Fact]
    public void AddOpeningHours_rejects_a_window_shorter_than_one_slot()
    {
        var branch = DomainTestData.NewBranch(slotLengthMinutes: 30);

        Assert.Throws<DomainException>(() =>
            branch.AddOpeningHours(DayOfWeek.Sunday, new TimeOnly(9, 0), new TimeOnly(9, 20)));
    }

    [Fact]
    public void Deactivate_and_Activate_toggle_availability()
    {
        var branch = DomainTestData.NewBranch();

        branch.Deactivate();
        Assert.False(branch.IsActive);

        branch.Activate();
        Assert.True(branch.IsActive);
    }
}
