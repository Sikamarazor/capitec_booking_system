using BranchBooking.Domain.Enums;
using BranchBooking.Domain.ValueObjects;
using BranchBooking.Infrastructure.Persistence;
using BranchBooking.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace BranchBooking.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public class BookingPersistenceTests(PostgresFixture db)
{
    private static readonly DateTimeOffset Now = PersistenceTestData.Now;
    private static readonly DateTimeOffset Start = Now.AddDays(1);

    private static void AssertPostgresError(DbUpdateException exception, string sqlState, string constraint)
    {
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(sqlState, postgres.SqlState);
        Assert.Equal(constraint, postgres.ConstraintName);
    }

    [Fact]
    public async Task Saves_and_reloads_a_branch_with_opening_hours()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, _) = await db.PrepareAsync();

        await using var context = db.CreateContext();
        var loaded = await context.Branches.Include(b => b.OpeningHours).SingleAsync(b => b.Id == branch.Id, ct);

        Assert.Equal("TEST-001", loaded.Code);
        Assert.Equal(6, loaded.OpeningHours.Count);
        var monday = Assert.Single(loaded.OpeningHours, h => h.DayOfWeek == DayOfWeek.Monday);
        Assert.Equal(new TimeOnly(8, 0), monday.OpensAt);
        Assert.Equal(new TimeOnly(17, 0), monday.ClosesAt);
        Assert.Equal(18, loaded.GetSlotsFor(new DateOnly(2026, 11, 2)).Count);
    }

    [Fact]
    public async Task Saves_and_reloads_a_booking_with_every_value()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var booking = PersistenceTestData.NewBooking(branch.Id, type.Id, Start, slotNumber: 2);

        await using (var write = db.CreateContext())
        {
            write.Bookings.Add(booking);
            await write.SaveChangesAsync(ct);
        }

        await using var read = db.CreateContext();
        var loaded = await read.Bookings.AsNoTracking().SingleAsync(b => b.Id == booking.Id, ct);

        Assert.Equal(booking.Reference, loaded.Reference);
        Assert.Equal(branch.Id, loaded.BranchId);
        Assert.Equal(type.Id, loaded.AppointmentTypeId);
        Assert.Equal(Start, loaded.StartsAtUtc);
        Assert.Equal(TimeSpan.Zero, loaded.StartsAtUtc.Offset);
        Assert.Equal(Start.AddMinutes(30), loaded.EndsAtUtc);
        Assert.Equal(2, loaded.SlotNumber);
        Assert.Equal(BookingStatus.Confirmed, loaded.Status);
        Assert.Equal(0, loaded.RescheduleCount);
        Assert.Equal(Now, loaded.CreatedAtUtc);
        Assert.Null(loaded.CancelledAtUtc);
        Assert.Equal("Thandi Nkosi", loaded.Customer.FullName);
        Assert.Equal("thandi@example.com", loaded.Customer.Email);
        Assert.Equal("0821234567", loaded.Customer.Phone);
    }

    [Fact]
    public async Task Rejects_a_second_confirmed_booking_in_the_same_position()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();

        await using (var first = db.CreateContext())
        {
            first.Bookings.Add(PersistenceTestData.NewBooking(branch.Id, type.Id, Start, slotNumber: 1));
            await first.SaveChangesAsync(ct);
        }

        await using var second = db.CreateContext();
        second.Bookings.Add(PersistenceTestData.NewBooking(branch.Id, type.Id, Start, slotNumber: 1));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync(ct));

        AssertPostgresError(exception, PostgresErrorCodes.UniqueViolation, DatabaseConstraints.ConfirmedSlotUnique);
    }

    [Fact]
    public async Task Allows_another_position_in_the_same_time_slot()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();

        await using var context = db.CreateContext();
        context.Bookings.Add(PersistenceTestData.NewBooking(branch.Id, type.Id, Start, slotNumber: 1));
        context.Bookings.Add(PersistenceTestData.NewBooking(branch.Id, type.Id, Start, slotNumber: 2));

        await context.SaveChangesAsync(ct);

        Assert.Equal(2, await context.Bookings.CountAsync(ct));
    }

    [Fact]
    public async Task Releases_the_position_when_the_booking_is_cancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var original = PersistenceTestData.NewBooking(branch.Id, type.Id, Start, slotNumber: 1);

        await using (var first = db.CreateContext())
        {
            first.Bookings.Add(original);
            await first.SaveChangesAsync(ct);
        }

        await using (var cancel = db.CreateContext())
        {
            var tracked = await cancel.Bookings.SingleAsync(b => b.Id == original.Id, ct);
            tracked.Cancel(Now.AddHours(1), "Plans changed");
            await cancel.SaveChangesAsync(ct);
        }

        await using var rebook = db.CreateContext();
        rebook.Bookings.Add(PersistenceTestData.NewBooking(branch.Id, type.Id, Start, slotNumber: 1));
        await rebook.SaveChangesAsync(ct);

        Assert.Equal(2, await rebook.Bookings.CountAsync(ct));
    }

    [Fact]
    public async Task Rejects_a_duplicate_booking_reference()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var reference = BookingReference.Generate();

        await using (var first = db.CreateContext())
        {
            first.Bookings.Add(PersistenceTestData.NewBooking(branch.Id, type.Id, Start, 1, reference));
            await first.SaveChangesAsync(ct);
        }

        await using var second = db.CreateContext();
        second.Bookings.Add(PersistenceTestData.NewBooking(branch.Id, type.Id, Start.AddHours(1), 1, reference));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync(ct));

        AssertPostgresError(exception, PostgresErrorCodes.UniqueViolation, DatabaseConstraints.BookingReferenceUnique);
    }

    [Fact]
    public async Task Rejects_a_booking_for_an_unknown_branch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, type) = await db.PrepareAsync();

        await using var context = db.CreateContext();
        context.Bookings.Add(PersistenceTestData.NewBooking(Guid.CreateVersion7(), type.Id, Start));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(ct));

        AssertPostgresError(exception, PostgresErrorCodes.ForeignKeyViolation, DatabaseConstraints.BookingBranchForeignKey);
    }

    [Fact]
    public async Task Detects_two_people_changing_the_same_booking()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var booking = PersistenceTestData.NewBooking(branch.Id, type.Id, Start);

        await using (var setup = db.CreateContext())
        {
            setup.Bookings.Add(booking);
            await setup.SaveChangesAsync(ct);
        }

        await using var first = db.CreateContext();
        await using var second = db.CreateContext();
        var firstCopy = await first.Bookings.SingleAsync(b => b.Id == booking.Id, ct);
        var secondCopy = await second.Bookings.SingleAsync(b => b.Id == booking.Id, ct);

        firstCopy.Cancel(Now.AddHours(1));
        await first.SaveChangesAsync(ct);

        secondCopy.Cancel(Now.AddHours(2));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task Database_rejects_a_slot_number_below_one()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var booking = PersistenceTestData.NewBooking(branch.Id, type.Id, Start);

        await using var context = db.CreateContext();
        context.Bookings.Add(booking);
        await context.SaveChangesAsync(ct);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlAsync($"UPDATE bookings SET slot_number = 0 WHERE id = {booking.Id}", ct));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_bookings_slot_number_positive", exception.ConstraintName);
    }

    [Fact]
    public async Task Database_rejects_a_cancelled_status_without_a_cancellation_time()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var booking = PersistenceTestData.NewBooking(branch.Id, type.Id, Start);

        await using var context = db.CreateContext();
        context.Bookings.Add(booking);
        await context.SaveChangesAsync(ct);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlAsync($"UPDATE bookings SET status = 'Cancelled' WHERE id = {booking.Id}", ct));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_bookings_cancelled_consistency", exception.ConstraintName);
    }

    [Fact]
    public async Task Finds_a_booking_by_reference_typed_in_lower_case_without_hyphens()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var booking = PersistenceTestData.NewBooking(branch.Id, type.Id, Start);

        await using (var setup = db.CreateContext())
        {
            setup.Bookings.Add(booking);
            await setup.SaveChangesAsync(ct);
        }

        var typed = booking.Reference.Value.Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        var reference = BookingReference.Parse(typed);

        await using var read = db.CreateContext();
        var found = await read.Bookings.SingleOrDefaultAsync(b => b.Reference == reference, ct);

        Assert.NotNull(found);
        Assert.Equal(booking.Id, found.Id);
    }
}
