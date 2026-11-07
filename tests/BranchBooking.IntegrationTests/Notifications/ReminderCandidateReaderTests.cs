using BranchBooking.Application.Abstractions;
using BranchBooking.Domain.Entities;
using BranchBooking.Infrastructure.Persistence.Repositories;
using BranchBooking.IntegrationTests.Support;
using Xunit;

namespace BranchBooking.IntegrationTests.Notifications;

[Collection(DatabaseCollection.Name)]
public class ReminderCandidateReaderTests(PostgresFixture db)
{
    private static readonly DateTimeOffset Now = PersistenceTestData.Now;

    /// <summary>Bookings made at <c>Now</c>; the window is 1 hour to 24 hours after <c>Now</c>, with the gap check wide open.</summary>
    private static ReminderQuery Window(int limit = 100, DateTimeOffset? updatedAtOrBefore = null) =>
        new(Now.AddHours(1), Now.AddHours(24), updatedAtOrBefore ?? Now, limit);

    private async Task<Booking> InsertAsync(Guid branchId, Guid typeId, DateTimeOffset startsAtUtc, int slotNumber = 1, bool cancelled = false)
    {
        var booking = PersistenceTestData.NewBooking(branchId, typeId, startsAtUtc, slotNumber);
        if (cancelled)
        {
            booking.Cancel(Now);
        }

        await using var context = db.CreateContext();
        context.Bookings.Add(booking);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return booking;
    }

    private async Task<IReadOnlyList<ReminderCandidate>> FindAsync(ReminderQuery query)
    {
        await using var context = db.CreateContext();
        return await new ReminderCandidateReader(context).FindDueAsync(query, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Returns_a_confirmed_booking_inside_the_window_with_its_start_time()
    {
        var (branch, type) = await db.PrepareAsync();
        var booking = await InsertAsync(branch.Id, type.Id, Now.AddHours(20));

        var found = await FindAsync(Window());

        var candidate = Assert.Single(found);
        Assert.Equal(booking.Id, candidate.BookingId);
        Assert.Equal(booking.StartsAtUtc, candidate.StartsAtUtc);
    }

    [Fact]
    public async Task The_window_is_exclusive_at_the_near_end_and_inclusive_at_the_far_end()
    {
        var (branch, type) = await db.PrepareAsync();
        await InsertAsync(branch.Id, type.Id, Now.AddHours(1), slotNumber: 1);          // exactly the minimum lead: out
        var atFarEnd = await InsertAsync(branch.Id, type.Id, Now.AddHours(24), slotNumber: 1);
        await InsertAsync(branch.Id, type.Id, Now.AddHours(24).AddMinutes(1), slotNumber: 1);   // beyond: out
        var inside = await InsertAsync(branch.Id, type.Id, Now.AddHours(1).AddMinutes(1), slotNumber: 1);

        var found = await FindAsync(Window());

        Assert.Equal([inside.Id, atFarEnd.Id], found.Select(c => c.BookingId));
    }

    [Fact]
    public async Task Leaves_out_cancelled_bookings()
    {
        var (branch, type) = await db.PrepareAsync();
        await InsertAsync(branch.Id, type.Id, Now.AddHours(20), cancelled: true);

        Assert.Empty(await FindAsync(Window()));
    }

    [Fact]
    public async Task Leaves_out_bookings_changed_inside_the_gap()
    {
        var (branch, type) = await db.PrepareAsync();
        await InsertAsync(branch.Id, type.Id, Now.AddHours(20));   // updated at Now

        Assert.Empty(await FindAsync(Window(updatedAtOrBefore: Now.AddMinutes(-1))));
        Assert.Single(await FindAsync(Window(updatedAtOrBefore: Now)));
    }

    [Fact]
    public async Task Leaves_out_a_booking_that_has_a_reminder_for_its_current_start_only()
    {
        var (branch, type) = await db.PrepareAsync();
        var reminded = await InsertAsync(branch.Id, type.Id, Now.AddHours(10));
        var movedSince = await InsertAsync(branch.Id, type.Id, Now.AddHours(12));

        await using (var context = db.CreateContext())
        {
            context.NotificationMessages.Add(NotificationMessage.ForReminder(reminded.Id, reminded.StartsAtUtc, Now));
            context.NotificationMessages.Add(NotificationMessage.ForReminder(movedSince.Id, movedSince.StartsAtUtc.AddHours(-5), Now));   // a reminder for an earlier time
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var found = await FindAsync(Window());

        var candidate = Assert.Single(found);
        Assert.Equal(movedSince.Id, candidate.BookingId);
    }

    [Fact]
    public async Task Returns_the_earliest_appointments_first_up_to_the_limit()
    {
        var (branch, type) = await db.PrepareAsync();
        var third = await InsertAsync(branch.Id, type.Id, Now.AddHours(15));
        var first = await InsertAsync(branch.Id, type.Id, Now.AddHours(5));
        var second = await InsertAsync(branch.Id, type.Id, Now.AddHours(10));

        var found = await FindAsync(Window(limit: 2));

        Assert.Equal([first.Id, second.Id], found.Select(c => c.BookingId));
        Assert.DoesNotContain(found, c => c.BookingId == third.Id);
    }

    [Fact]
    public async Task A_partial_index_on_confirmed_start_times_exists()
    {
        await db.PrepareAsync();

        await using var command = db.DataSource.CreateCommand(
            "select indexdef from pg_indexes where indexname = 'ix_bookings_confirmed_start'");
        var definition = (string?)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(definition);
        Assert.Contains("starts_at_utc", definition, StringComparison.Ordinal);
        Assert.Contains("Confirmed", definition, StringComparison.Ordinal);
    }
}
