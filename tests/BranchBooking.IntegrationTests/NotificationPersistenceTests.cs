using BranchBooking.Application.Abstractions;
using BranchBooking.Domain.Entities;
using BranchBooking.Domain.Enums;
using BranchBooking.Infrastructure.Persistence;
using BranchBooking.Infrastructure.Persistence.Repositories;
using BranchBooking.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace BranchBooking.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public class NotificationPersistenceTests(PostgresFixture db)
{
    private static readonly DateTimeOffset TuesdayEightAm = new(2026, 11, 3, 6, 0, 0, TimeSpan.Zero);

    private async Task<Booking> InsertBookingAsync(Guid branchId, Guid typeId)
    {
        var booking = PersistenceTestData.NewBooking(branchId, typeId, TuesdayEightAm);
        await using var context = db.CreateContext();
        context.Bookings.Add(booking);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return booking;
    }

    [Fact]
    public async Task A_message_round_trips_with_kind_and_status_stored_as_text()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var booking = await InsertBookingAsync(branch.Id, type.Id);
        var message = NotificationMessage.ForConfirmation(booking, PersistenceTestData.Now);

        await using (var write = db.CreateContext())
        {
            write.NotificationMessages.Add(message);
            await write.SaveChangesAsync(ct);
        }

        await using var read = db.CreateContext();
        var loaded = await read.NotificationMessages.SingleAsync(ct);
        Assert.Equal(message.Id, loaded.Id);
        Assert.Equal(NotificationKind.Confirmation, loaded.Kind);
        Assert.Equal(NotificationStatus.Pending, loaded.Status);
        Assert.Equal(message.DedupeKey, loaded.DedupeKey);
        Assert.Equal(message.StartsAtUtc, loaded.StartsAtUtc);

        var text = await read.Database.SqlQueryRaw<string>("SELECT kind || '/' || status AS \"Value\" FROM notification_messages").SingleAsync(ct);
        Assert.Equal("Confirmation/Pending", text);
    }

    [Fact]
    public async Task A_booking_and_its_message_are_written_by_one_save()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        await using var context = db.CreateContext();
        var booking = PersistenceTestData.NewBooking(branch.Id, type.Id, TuesdayEightAm);
        new BookingRepository(context).Add(booking);
        new NotificationRepository(context).Add(NotificationMessage.ForConfirmation(booking, PersistenceTestData.Now));

        await new EfUnitOfWork(context).SaveChangesAsync(ct);

        await using var read = db.CreateContext();
        Assert.Equal(1, await read.Bookings.CountAsync(ct));
        Assert.Equal(booking.Id, (await read.NotificationMessages.SingleAsync(ct)).BookingId);
    }

    [Fact]
    public async Task A_failed_booking_save_leaves_no_message()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync(slotCapacity: 1);
        await InsertBookingAsync(branch.Id, type.Id);   // takes position 1

        await using var context = db.CreateContext();
        var rival = PersistenceTestData.NewBooking(branch.Id, type.Id, TuesdayEightAm);   // also position 1
        new BookingRepository(context).Add(rival);
        new NotificationRepository(context).Add(NotificationMessage.ForConfirmation(rival, PersistenceTestData.Now));

        await Assert.ThrowsAsync<SlotAlreadyBookedException>(() => new EfUnitOfWork(context).SaveChangesAsync(ct));

        await using var read = db.CreateContext();
        Assert.Equal(0, await read.NotificationMessages.CountAsync(ct));
    }

    [Fact]
    public async Task Queuing_the_same_notification_twice_is_reported_as_already_queued()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var booking = await InsertBookingAsync(branch.Id, type.Id);

        await using (var first = db.CreateContext())
        {
            new NotificationRepository(first).Add(NotificationMessage.ForReminder(booking.Id, booking.StartsAtUtc, PersistenceTestData.Now));
            await new EfUnitOfWork(first).SaveChangesAsync(ct);
        }

        await using var second = db.CreateContext();
        new NotificationRepository(second).Add(NotificationMessage.ForReminder(booking.Id, booking.StartsAtUtc, PersistenceTestData.Now));

        await Assert.ThrowsAsync<NotificationAlreadyQueuedException>(() => new EfUnitOfWork(second).SaveChangesAsync(ct));
        Assert.Empty(second.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Deleting_a_booking_deletes_its_messages()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var booking = await InsertBookingAsync(branch.Id, type.Id);
        await using (var write = db.CreateContext())
        {
            write.NotificationMessages.Add(NotificationMessage.ForConfirmation(booking, PersistenceTestData.Now));
            await write.SaveChangesAsync(ct);
        }

        await using var context = db.CreateContext();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM bookings", ct);

        Assert.Equal(0, await context.NotificationMessages.CountAsync(ct));
    }

    [Fact]
    public async Task The_database_refuses_a_sent_message_with_no_sent_time()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var booking = await InsertBookingAsync(branch.Id, type.Id);
        await using (var write = db.CreateContext())
        {
            write.NotificationMessages.Add(NotificationMessage.ForConfirmation(booking, PersistenceTestData.Now));
            await write.SaveChangesAsync(ct);
        }

        await using var context = db.CreateContext();
        var failure = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlRawAsync("UPDATE notification_messages SET status = 'Sent'", ct));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
    }

    [Fact]
    public async Task Listing_returns_only_that_bookings_messages_oldest_first()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var booking = await InsertBookingAsync(branch.Id, type.Id);
        var other = PersistenceTestData.NewBooking(branch.Id, type.Id, TuesdayEightAm.AddMinutes(30));
        await using (var write = db.CreateContext())
        {
            write.Bookings.Add(other);
            write.NotificationMessages.Add(NotificationMessage.ForCancellation(booking, PersistenceTestData.Now.AddMinutes(5)));
            write.NotificationMessages.Add(NotificationMessage.ForConfirmation(booking, PersistenceTestData.Now));
            write.NotificationMessages.Add(NotificationMessage.ForConfirmation(other, PersistenceTestData.Now));
            await write.SaveChangesAsync(ct);
        }

        await using var read = db.CreateContext();
        var messages = await new NotificationRepository(read).ListForBookingAsync(booking.Id, ct);

        Assert.Equal([NotificationKind.Confirmation, NotificationKind.Cancellation], messages.Select(m => m.Kind));
    }
}
