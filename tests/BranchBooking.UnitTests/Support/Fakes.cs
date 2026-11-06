using BranchBooking.Application.Abstractions;
using BranchBooking.Domain.Entities;
using BranchBooking.Domain.Enums;
using BranchBooking.Domain.ValueObjects;

namespace BranchBooking.UnitTests.Support;

/// <summary>Shared state behind the in-memory repositories and unit of work.</summary>
internal sealed class InMemoryStore
{
    public List<Booking> Bookings { get; } = [];

    /// <summary>Added through the repository, not yet "saved".</summary>
    public List<Booking> Staged { get; } = [];

    /// <summary>Each save dequeues one failure and throws it (after clearing <see cref="Staged"/>), like a real conflict.</summary>
    public Queue<Exception> SaveFailures { get; } = new();

    /// <summary>Runs when a queued failure fires, to simulate what the competing request changed.</summary>
    public Action? OnSaveFailure { get; set; }

    public int SaveCalls { get; set; }
}

internal sealed class InMemoryBranchRepository(params Branch[] branches) : IBranchRepository
{
    public Task<IReadOnlyList<Branch>> ListActiveAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Branch>>(branches.Where(b => b.IsActive).OrderBy(b => b.Name).ToList());

    public Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(branches.FirstOrDefault(b => b.Id == id));
}

internal sealed class InMemoryAppointmentTypeRepository(params AppointmentType[] types) : IAppointmentTypeRepository
{
    public Task<IReadOnlyList<AppointmentType>> ListActiveAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AppointmentType>>(
            types.Where(t => t.IsActive).OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToList());

    public Task<AppointmentType?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(types.FirstOrDefault(t => t.Id == id));
}

internal sealed class InMemoryBookingRepository(InMemoryStore store) : IBookingRepository
{
    public void Add(Booking booking) => store.Staged.Add(booking);

    public Task<Booking?> GetByReferenceAsync(BookingReference reference, CancellationToken cancellationToken) =>
        Task.FromResult(store.Bookings.FirstOrDefault(b => b.Reference == reference));

    public Task<IReadOnlyList<BookedPosition>> GetConfirmedPositionsAsync(
        Guid branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        IReadOnlyList<BookedPosition> positions = store.Bookings
            .Where(b => b.BranchId == branchId
                        && b.Status == BookingStatus.Confirmed
                        && b.StartsAtUtc >= fromUtc
                        && b.StartsAtUtc < toUtc)
            .OrderBy(b => b.StartsAtUtc)
            .ThenBy(b => b.SlotNumber)
            .Select(b => new BookedPosition(b.StartsAtUtc, b.SlotNumber))
            .ToList();

        return Task.FromResult(positions);
    }
}

internal sealed class FakeUnitOfWork(InMemoryStore store) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        store.SaveCalls++;

        if (store.SaveFailures.Count > 0)
        {
            var failure = store.SaveFailures.Dequeue();
            store.Staged.Clear();
            store.OnSaveFailure?.Invoke();
            throw failure;
        }

        store.Bookings.AddRange(store.Staged);
        store.Staged.Clear();
        return Task.CompletedTask;
    }
}

internal static class BookingTestExtensions
{
    /// <summary>
    /// A booking loaded by the real repository has its branch and appointment type attached; the fake
    /// repository returns bookings created in memory, so attach them the same way (the setters are private).
    /// </summary>
    public static void AttachNavigation(this Booking booking, Branch branch, AppointmentType type)
    {
        typeof(Booking).GetProperty(nameof(Booking.Branch))!.SetValue(booking, branch);
        typeof(Booking).GetProperty(nameof(Booking.AppointmentType))!.SetValue(booking, type);
    }
}
