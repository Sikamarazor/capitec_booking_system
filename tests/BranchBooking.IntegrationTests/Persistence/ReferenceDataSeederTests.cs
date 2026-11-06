using BranchBooking.Infrastructure.Persistence.Seeding;
using BranchBooking.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BranchBooking.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public class ReferenceDataSeederTests(PostgresFixture db)
{
    private async Task SeedAsync(CancellationToken ct)
    {
        await using var context = db.CreateContext();
        await new ReferenceDataSeeder(context, NullLogger<ReferenceDataSeeder>.Instance).SeedAsync(ct);
    }

    [Fact]
    public async Task Seeds_an_empty_database()
    {
        var ct = TestContext.Current.CancellationToken;
        await db.ResetAsync();

        await SeedAsync(ct);

        await using var read = db.CreateContext();
        Assert.Equal(3, await read.AppointmentTypes.CountAsync(ct));
        Assert.Equal(5, await read.Branches.CountAsync(ct));
    }

    [Fact]
    public async Task Seeded_branches_load_with_their_opening_hours()
    {
        var ct = TestContext.Current.CancellationToken;
        await db.ResetAsync();

        await SeedAsync(ct);

        await using var read = db.CreateContext();
        var branches = await read.Branches.Include(b => b.OpeningHours).ToListAsync(ct);
        Assert.All(branches, b => Assert.Equal(6, b.OpeningHours.Count));
    }

    [Fact]
    public async Task Running_twice_does_not_duplicate_anything()
    {
        var ct = TestContext.Current.CancellationToken;
        await db.ResetAsync();

        await SeedAsync(ct);
        await SeedAsync(ct);

        await using var read = db.CreateContext();
        Assert.Equal(3, await read.AppointmentTypes.CountAsync(ct));
        Assert.Equal(5, await read.Branches.CountAsync(ct));
    }

    [Fact]
    public async Task Does_not_overwrite_rows_that_were_edited_after_seeding()
    {
        var ct = TestContext.Current.CancellationToken;
        await db.ResetAsync();
        await SeedAsync(ct);

        await using (var edit = db.CreateContext())
        {
            await edit.Database.ExecuteSqlAsync($"UPDATE branches SET name = 'Renamed by the business' WHERE code = 'DEMO-JHB-01'", ct);
        }

        await SeedAsync(ct);

        await using var read = db.CreateContext();
        var branch = await read.Branches.SingleAsync(b => b.Code == "DEMO-JHB-01", ct);
        Assert.Equal("Renamed by the business", branch.Name);
        Assert.Equal(5, await read.Branches.CountAsync(ct));
    }

    [Fact]
    public async Task Adds_only_the_rows_that_are_missing()
    {
        var ct = TestContext.Current.CancellationToken;
        await db.ResetAsync();
        await SeedAsync(ct);

        await using (var edit = db.CreateContext())
        {
            await edit.Database.ExecuteSqlAsync($"DELETE FROM appointment_types WHERE code = 'CARD_COLLECTION'", ct);
        }

        await SeedAsync(ct);

        await using var read = db.CreateContext();
        Assert.Equal(3, await read.AppointmentTypes.CountAsync(ct));
        Assert.Equal(5, await read.Branches.CountAsync(ct));
    }
}
