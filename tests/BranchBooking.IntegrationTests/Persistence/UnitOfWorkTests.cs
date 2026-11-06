using BranchBooking.Application.Abstractions;
using BranchBooking.Domain.Entities;
using BranchBooking.Domain.Enums;
using BranchBooking.Domain.ValueObjects;
using BranchBooking.Infrastructure.Persistence;
using BranchBooking.Infrastructure.Persistence.Repositories;
using BranchBooking.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BranchBooking.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public class UnitOfWorkTests(PostgresFixture db)
{
    private static readonly DateTimeOffset Now = PersistenceTestData.Now;
    private static readonly DateTimeOffset Start = Now.AddDays(1);

    private async Task SaveBookingAsync(Booking booking, CancellationToken ct)
    {
        await using var context = db.CreateContext();
        new BookingRepository(context).Add(booking);
        await new EfUnitOfWork(context).SaveChangesAsync(ct);
    }

    [Fact]
    public async Task Saves_staged_changes()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var booking = PersistenceTestData.NewBooking(branch.Id, type.Id, Start);

        await SaveBookingAsync(booking, ct);

        await using var read = db.CreateContext();
        var found = await new BookingRepository(read).GetByReferenceAsync(booking.Reference, ct);
        Assert.NotNull(found);
        Assert.Equal(BookingStatus.Confirmed, found.Status);
    }

    [Fact]
    public async Task Translates_a_taken_position_into_SlotAlreadyBookedException()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        await SaveBookingAsync(PersistenceTestData.NewBooking(branch.Id, type.Id, Start, slotNumber: 1), ct);

        await using var context = db.CreateContext();
        new BookingRepository(context).Add(PersistenceTestData.NewBooking(branch.Id, type.Id, Start, slotNumber: 1));

        var exception = await Assert.ThrowsAsync<SlotAlreadyBookedException>(() => new EfUnitOfWork(context).SaveChangesAsync(ct));

        Assert.IsType<DbUpdateException>(exception.InnerException);
    }

    [Fact]
    public async Task Translates_a_repeated_reference_into_DuplicateBookingReferenceException()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var reference = BookingReference.Generate();
        await SaveBookingAsync(PersistenceTestData.NewBooking(branch.Id, type.Id, Start, 1, reference), ct);

        await using var context = db.CreateContext();
        new BookingRepository(context).Add(PersistenceTestData.NewBooking(branch.Id, type.Id, Start.AddHours(1), 1, reference));

        await Assert.ThrowsAsync<DuplicateBookingReferenceException>(() => new EfUnitOfWork(context).SaveChangesAsync(ct));
    }

    [Fact]
    public async Task Translates_a_lost_update_into_ConcurrencyConflictException()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var booking = PersistenceTestData.NewBooking(branch.Id, type.Id, Start);
        await SaveBookingAsync(booking, ct);

        await using var firstContext = db.CreateContext();
        await using var secondContext = db.CreateContext();
        var first = await new BookingRepository(firstContext).GetByReferenceAsync(booking.Reference, ct);
        var second = await new BookingRepository(secondContext).GetByReferenceAsync(booking.Reference, ct);
        Assert.NotNull(first);
        Assert.NotNull(second);

        first.Cancel(Now.AddHours(1));
        await new EfUnitOfWork(firstContext).SaveChangesAsync(ct);

        second.Cancel(Now.AddHours(2));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => new EfUnitOfWork(secondContext).SaveChangesAsync(ct));
    }

    [Fact]
    public async Task The_same_context_can_be_used_again_after_a_conflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        await SaveBookingAsync(PersistenceTestData.NewBooking(branch.Id, type.Id, Start, slotNumber: 1), ct);

        await using var context = db.CreateContext();
        var repository = new BookingRepository(context);
        var unitOfWork = new EfUnitOfWork(context);

        repository.Add(PersistenceTestData.NewBooking(branch.Id, type.Id, Start, slotNumber: 1));
        await Assert.ThrowsAsync<SlotAlreadyBookedException>(() => unitOfWork.SaveChangesAsync(ct));

        var retry = PersistenceTestData.NewBooking(branch.Id, type.Id, Start, slotNumber: 2);
        repository.Add(retry);
        await unitOfWork.SaveChangesAsync(ct);

        await using var read = db.CreateContext();
        Assert.NotNull(await new BookingRepository(read).GetByReferenceAsync(retry.Reference, ct));
    }
}
