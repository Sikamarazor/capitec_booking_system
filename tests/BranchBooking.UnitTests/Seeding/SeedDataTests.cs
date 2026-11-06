using BranchBooking.Infrastructure.Persistence.Seeding;
using Xunit;

namespace BranchBooking.UnitTests.Seeding;

public class SeedDataTests
{
    [Fact]
    public void Appointment_types_include_the_three_launch_types_with_unique_codes()
    {
        var types = SeedData.AppointmentTypes();

        Assert.Equal(["OPEN_ACCOUNT", "CARD_COLLECTION", "LOAN_APPLICATION"], types.Select(t => t.Code));
        Assert.Equal(types.Count, types.Select(t => t.Code).Distinct().Count());
        Assert.All(types, t => Assert.True(t.IsActive));
    }

    [Fact]
    public void Branches_have_unique_codes_and_valid_configuration()
    {
        var branches = SeedData.Branches();

        Assert.Equal(5, branches.Count);
        Assert.Equal(branches.Count, branches.Select(b => b.Code).Distinct().Count());
        Assert.All(branches, b =>
        {
            Assert.StartsWith("DEMO-", b.Code, StringComparison.Ordinal);
            Assert.Equal("Africa/Johannesburg", b.TimeZoneId);
            Assert.InRange(b.SlotCapacity, 1, 3);
        });
    }

    [Fact]
    public void Every_branch_is_open_monday_to_saturday_and_closed_on_sunday()
    {
        Assert.All(SeedData.Branches(), branch =>
        {
            Assert.Equal(6, branch.OpeningHours.Count);
            Assert.Empty(branch.GetSlotsFor(new DateOnly(2026, 11, 8)));    // Sunday
            Assert.Equal(18, branch.GetSlotsFor(new DateOnly(2026, 11, 2)).Count);   // Monday
            Assert.Equal(10, branch.GetSlotsFor(new DateOnly(2026, 11, 7)).Count);   // Saturday
        });
    }

    [Fact]
    public void Each_call_returns_new_instances_with_new_ids()
    {
        var first = SeedData.Branches();
        var second = SeedData.Branches();

        Assert.NotEqual(first[0].Id, second[0].Id);
        Assert.Equal(first[0].Code, second[0].Code);
    }
}
