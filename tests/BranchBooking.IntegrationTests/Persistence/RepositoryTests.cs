using BranchBooking.Application.Abstractions;
using BranchBooking.Domain.ValueObjects;
using BranchBooking.Infrastructure.Persistence.Repositories;
using BranchBooking.IntegrationTests.Support;
using Xunit;

namespace BranchBooking.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public class RepositoryTests(PostgresFixture db)
{
    private static readonly DateTimeOffset Now = PersistenceTestData.Now;
    private static readonly DateTimeOffset Start = Now.AddDays(1);

    [Fact]
    public async Task Branches_list_only_active_ones_ordered_by_name_with_opening_hours()
    {
        var ct = TestContext.Current.CancellationToken;
        await db.PrepareAsync();   // "Test Branch"

        var alpha = PersistenceTestData.NewBranch("ALPHA-01", "Alpha Branch");
        var closed = PersistenceTestData.NewBranch("CLOSED-01", "Closed Branch");
        closed.Deactivate();
        await using (var setup = db.CreateContext())
        {
            setup.Branches.AddRange(alpha, closed);
            await setup.SaveChangesAsync(ct);
        }

        await using var context = db.CreateContext();
        var result = await new BranchRepository(context).ListActiveAsync(ct);

        Assert.Equal(["Alpha Branch", "Test Branch"], result.Select(b => b.Name));
        Assert.All(result, b => Assert.Equal(6, b.OpeningHours.Count));
    }

    [Fact]
    public async Task Branch_by_id_returns_it_with_opening_hours_and_null_when_unknown()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, _) = await db.PrepareAsync();

        await using var context = db.CreateContext();
        var repository = new BranchRepository(context);

        var found = await repository.GetByIdAsync(branch.Id, ct);
        var missing = await repository.GetByIdAsync(Guid.CreateVersion7(), ct);

        Assert.NotNull(found);
        Assert.Equal(6, found.OpeningHours.Count);
        Assert.Null(missing);
    }

    [Fact]
    public async Task Appointment_types_list_only_active_ones_ordered_by_sort_order()
    {
        var ct = TestContext.Current.CancellationToken;
        await db.PrepareAsync();   // "Test type", sort order 1

        var first = PersistenceTestData.NewType("FIRST_TYPE", "First type", sortOrder: 0);
        var hidden = PersistenceTestData.NewType("HIDDEN_TYPE", "Hidden type", sortOrder: 2);
        hidden.Deactivate();
        await using (var setup = db.CreateContext())
        {
            setup.AppointmentTypes.AddRange(first, hidden);
            await setup.SaveChangesAsync(ct);
        }

        await using var context = db.CreateContext();
        var result = await new AppointmentTypeRepository(context).ListActiveAsync(ct);

        Assert.Equal(["First type", "Test type"], result.Select(t => t.Name));
    }

    [Fact]
    public async Task Appointment_type_by_id_returns_it_and_null_when_unknown()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, type) = await db.PrepareAsync();

        await using var context = db.CreateContext();
        var repository = new AppointmentTypeRepository(context);

        Assert.Equal("TEST_TYPE", (await repository.GetByIdAsync(type.Id, ct))?.Code);
        Assert.Null(await repository.GetByIdAsync(Guid.CreateVersion7(), ct));
    }

    [Fact]
    public async Task Booking_by_reference_returns_the_booking_with_branch_hours_and_type()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var booking = PersistenceTestData.NewBooking(branch.Id, type.Id, Start);
        await using (var setup = db.CreateContext())
        {
            setup.Bookings.Add(booking);
            await setup.SaveChangesAsync(ct);
        }

        var typed = BookingReference.Parse(booking.Reference.Value.ToLowerInvariant());

        await using var context = db.CreateContext();
        var found = await new BookingRepository(context).GetByReferenceAsync(typed, ct);

        Assert.NotNull(found);
        Assert.Equal(booking.Id, found.Id);
        Assert.Equal("TEST-001", found.Branch.Code);
        Assert.Equal(6, found.Branch.OpeningHours.Count);
        Assert.Equal("TEST_TYPE", found.AppointmentType.Code);
    }

    [Fact]
    public async Task Booking_by_reference_returns_null_when_unknown()
    {
        var ct = TestContext.Current.CancellationToken;
        await db.PrepareAsync();

        await using var context = db.CreateContext();
        var found = await new BookingRepository(context).GetByReferenceAsync(BookingReference.Generate(), ct);

        Assert.Null(found);
    }

    [Fact]
    public async Task Confirmed_positions_cover_only_confirmed_bookings_of_that_branch_inside_the_window()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var other = PersistenceTestData.NewBranch("OTHER-01", "Other Branch");

        var inWindowSecond = PersistenceTestData.NewBooking(branch.Id, type.Id, Start, slotNumber: 2);
        var inWindowFirst = PersistenceTestData.NewBooking(branch.Id, type.Id, Start, slotNumber: 1);
        var laterInWindow = PersistenceTestData.NewBooking(branch.Id, type.Id, Start.AddMinutes(30), slotNumber: 1);
        var willBeCancelled = PersistenceTestData.NewBooking(branch.Id, type.Id, Start.AddMinutes(60), slotNumber: 1);
        var outsideWindow = PersistenceTestData.NewBooking(branch.Id, type.Id, Start.AddDays(2), slotNumber: 1);
        var otherBranch = PersistenceTestData.NewBooking(other.Id, type.Id, Start, slotNumber: 1);
        willBeCancelled.Cancel(Now.AddHours(1));

        await using (var setup = db.CreateContext())
        {
            setup.Branches.Add(other);
            setup.Bookings.AddRange(inWindowSecond, inWindowFirst, laterInWindow, willBeCancelled, outsideWindow, otherBranch);
            await setup.SaveChangesAsync(ct);
        }

        await using var context = db.CreateContext();
        var positions = await new BookingRepository(context)
            .GetConfirmedPositionsAsync(branch.Id, Start, Start.AddHours(2), ct);

        Assert.Equal(
            [
                new BookedPosition(Start, 1),
                new BookedPosition(Start, 2),
                new BookedPosition(Start.AddMinutes(30), 1),
            ],
            positions);
    }
}
