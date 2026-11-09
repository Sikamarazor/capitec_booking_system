using BranchBooking.Application.Catalog;
using BranchBooking.UnitTests.Support;
using Xunit;

namespace BranchBooking.UnitTests.Catalog;

public class ListBranchesTests
{
    [Fact]
    public async Task Maps_the_branch_with_opening_hours_listed_monday_first()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();

        var result = await world.ListBranches().ExecuteAsync(ct);

        var dto = Assert.Single(result);
        Assert.Equal(world.Branch.Id, dto.Id);
        Assert.Equal("TEST-001", dto.Code);
        Assert.Equal("Africa/Johannesburg", dto.TimeZoneId);
        Assert.Equal(30, dto.SlotLengthMinutes);
        Assert.Equal(
            ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"],
            dto.OpeningHours.Select(h => h.Day));
        Assert.Equal(new OpeningHoursDto("Monday", "08:00", "17:00"), dto.OpeningHours[0]);
        Assert.Equal(new OpeningHoursDto("Saturday", "08:00", "13:00"), dto.OpeningHours[5]);
    }

    [Fact]
    public async Task Leaves_out_inactive_branches()
    {
        var ct = TestContext.Current.CancellationToken;
        var closed = DomainTestData_NewClosedBranch();
        var open = DomainTestData.NewBranch();
        var useCase = new ListBranches(new InMemoryBranchRepository(closed, open));

        var result = await useCase.ExecuteAsync(ct);

        var dto = Assert.Single(result);
        Assert.Equal(open.Id, dto.Id);
    }

    private static BranchBooking.Domain.Entities.Branch DomainTestData_NewClosedBranch()
    {
        var branch = DomainTestData.NewBranch();
        branch.Deactivate();
        return branch;
    }
}
