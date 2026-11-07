using BranchBooking.Application.Abstractions;
using BranchBooking.Domain.Entities;
using BranchBooking.Domain.Enums;
using BranchBooking.Infrastructure.Persistence;
using BranchBooking.Infrastructure.Persistence.Repositories;
using BranchBooking.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BranchBooking.IntegrationTests.Notifications;

[Collection(DatabaseCollection.Name)]
public class NotificationQueueTests(PostgresFixture db)
{
    private static readonly DateTimeOffset Now = PersistenceTestData.Now;
    private static readonly DateTimeOffset TuesdayEightAm = new(2026, 11, 3, 6, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    private NotificationQueue NewQueue(BranchBookingDbContext context) => new(context, db.DataSource);

    private async Task<Booking> InsertBookingAsync(Guid branchId, Guid typeId)
    {
        var booking = PersistenceTestData.NewBooking(branchId, typeId, TuesdayEightAm);
        await using var context = db.CreateContext();
        context.Bookings.Add(booking);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return booking;
    }

    private async Task InsertAsync(params NotificationMessage[] messages)
    {
        await using var context = db.CreateContext();
        context.NotificationMessages.AddRange(messages);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>One reminder per index, each due at a different minute.</summary>
    private static NotificationMessage Due(Booking booking, int index) =>
        NotificationMessage.ForReminder(booking.Id, booking.StartsAtUtc.AddMinutes(index), Now.AddMinutes(-index));

    [Fact]
    public async Task Claims_a_due_message_with_its_booking_branch_and_type_loaded()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var booking = await InsertBookingAsync(branch.Id, type.Id);
        var message = NotificationMessage.ForConfirmation(booking, Now);
        await InsertAsync(message);

        await using var context = db.CreateContext();
        var claimed = await NewQueue(context).ClaimDueAsync(Now, 10, Lease, ct);

        var loaded = Assert.Single(claimed);
        Assert.Equal(message.Id, loaded.Id);
        Assert.Equal(booking.Reference, loaded.Booking.Reference);
        Assert.Equal(branch.Name, loaded.Booking.Branch.Name);
        Assert.Equal(type.Code, loaded.Booking.AppointmentType.Code);
        Assert.Equal(EntityState.Unchanged, context.Entry(loaded).State);
    }

    [Fact]
    public async Task Leaves_out_messages_that_are_not_due_or_not_pending()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var booking = await InsertBookingAsync(branch.Id, type.Id);

        var due = Due(booking, 0);
        var later = Due(booking, 1);
        later.RecordFailure("brevo_500", true, Now, 5, TimeSpan.FromHours(1));
        var sent = Due(booking, 2);
        sent.MarkSent("Simulated", null, null, null, Now);
        var skipped = Due(booking, 3);
        skipped.MarkSkipped("superseded");
        var failed = Due(booking, 4);
        failed.RecordFailure("brevo_400", false, Now, 5, TimeSpan.Zero);
        await InsertAsync(due, later, sent, skipped, failed);

        await using var context = db.CreateContext();
        var claimed = await NewQueue(context).ClaimDueAsync(Now, 10, Lease, ct);

        Assert.Equal([due.Id], claimed.Select(m => m.Id));
    }

    [Fact]
    public async Task A_claimed_message_is_leased_until_the_lease_expires()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var booking = await InsertBookingAsync(branch.Id, type.Id);
        await InsertAsync(NotificationMessage.ForConfirmation(booking, Now));

        await using var first = db.CreateContext();
        await using var second = db.CreateContext();
        await using var third = db.CreateContext();
        var claimedNow = await NewQueue(first).ClaimDueAsync(Now, 10, Lease, ct);
        var claimedAgain = await NewQueue(second).ClaimDueAsync(Now.AddMinutes(1), 10, Lease, ct);
        var claimedAfterExpiry = await NewQueue(third).ClaimDueAsync(Now.AddMinutes(6), 10, Lease, ct);

        Assert.Single(claimedNow);
        Assert.Empty(claimedAgain);
        Assert.Single(claimedAfterExpiry);
    }

    [Fact]
    public async Task Respects_the_batch_size_and_takes_the_oldest_first()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var booking = await InsertBookingAsync(branch.Id, type.Id);
        var messages = Enumerable.Range(0, 5).Select(i => Due(booking, i)).ToArray();   // index 4 is the oldest
        await InsertAsync(messages);

        await using var context = db.CreateContext();
        var claimed = await NewQueue(context).ClaimDueAsync(Now, 3, Lease, ct);

        Assert.Equal(
            [messages[4].Id, messages[3].Id, messages[2].Id],
            claimed.Select(m => m.Id));
    }

    [Fact]
    public async Task Concurrent_claims_never_hand_out_the_same_message_twice()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var booking = await InsertBookingAsync(branch.Id, type.Id);
        await InsertAsync(Enumerable.Range(0, 12).Select(i => Due(booking, i)).ToArray());

        var claims = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var context = db.CreateContext();
            var claimed = await NewQueue(context).ClaimDueAsync(Now, 4, Lease, ct);
            return claimed.Select(m => m.Id).ToList();
        }));

        var ids = claims.SelectMany(c => c).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());

        // Whatever the parallel claims left behind is still claimable, and nothing is lost.
        await using var rest = db.CreateContext();
        var remaining = await NewQueue(rest).ClaimDueAsync(Now, 50, Lease, ct);
        Assert.Equal(12, ids.Count + remaining.Count);
    }

    [Fact]
    public async Task A_claimed_message_can_be_changed_and_saved()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var booking = await InsertBookingAsync(branch.Id, type.Id);
        var message = NotificationMessage.ForConfirmation(booking, Now);
        await InsertAsync(message);

        await using (var context = db.CreateContext())
        {
            var claimed = Assert.Single(await NewQueue(context).ClaimDueAsync(Now, 10, Lease, ct));
            claimed.MarkSent("Simulated", "sim-1", "S", "B", Now);
            await new EfUnitOfWork(context).SaveChangesAsync(ct);
        }

        await using var read = db.CreateContext();
        var saved = await read.NotificationMessages.SingleAsync(ct);
        Assert.Equal(NotificationStatus.Sent, saved.Status);
        Assert.Null(saved.LockedUntilUtc);
        Assert.Equal("sim-1", saved.ProviderMessageId);
    }
}
