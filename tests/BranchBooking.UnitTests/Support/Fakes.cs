using BranchBooking.Application.Abstractions;
using BranchBooking.Domain.Entities;
using BranchBooking.Domain.Enums;
using BranchBooking.Domain.ValueObjects;
using BranchBooking.Application.Notifications;

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

    public List<NotificationMessage> Messages { get; } = [];

    /// <summary>Added through the repository, not yet "saved".</summary>
    public List<NotificationMessage> StagedMessages { get; } = [];

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
            store.StagedMessages.Clear();
            store.OnSaveFailure?.Invoke();
            throw failure;
        }

        store.Bookings.AddRange(store.Staged);
        store.Staged.Clear();
        store.Messages.AddRange(store.StagedMessages);
        store.StagedMessages.Clear();
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

    /// <summary>Attaches the booking the way the real queue's query does (the setter is private).</summary>
    public static void AttachBooking(this NotificationMessage message, Booking booking) =>
        typeof(NotificationMessage).GetProperty(nameof(NotificationMessage.Booking))!.SetValue(message, booking);

}

internal sealed class InMemoryNotificationRepository(InMemoryStore store) : INotificationRepository
{
    public void Add(NotificationMessage message) => store.StagedMessages.Add(message);

    public Task<IReadOnlyList<NotificationMessage>> ListForBookingAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        IReadOnlyList<NotificationMessage> messages = store.Messages
            .Where(m => m.BookingId == bookingId)
            .OrderBy(m => m.CreatedAtUtc)
            .ThenBy(m => m.Id)
            .ToList();

        return Task.FromResult(messages);
    }
}

/// <summary>
/// Mimics the real claim: due pending messages, oldest first, up to the batch size, with their booking attached.
/// It does not model leases; the integration tests prove those against a real database.
/// </summary>
internal sealed class InMemoryNotificationQueue(InMemoryStore store) : INotificationQueue
{
    public Task<IReadOnlyList<NotificationMessage>> ClaimDueAsync(
        DateTimeOffset nowUtc, int batchSize, TimeSpan lease, CancellationToken cancellationToken)
    {
        var due = store.Messages
            .Where(m => m.Status == NotificationStatus.Pending && m.NextAttemptAtUtc <= nowUtc)
            .OrderBy(m => m.NextAttemptAtUtc)
            .ThenBy(m => m.Id)
            .Take(batchSize)
            .ToList();

        foreach (var message in due)
        {
            message.AttachBooking(store.Bookings.Single(b => b.Id == message.BookingId));
        }

        return Task.FromResult<IReadOnlyList<NotificationMessage>>(due);
    }
}

/// <summary>A sender that records what it was given and returns scripted results (success when the script is empty).</summary>
internal sealed class ScriptedSender : INotificationSender
{
    private readonly Queue<object> _script = new();

    public string Channel { get; set; } = "Simulated";

    public bool RetainsContent { get; set; } = true;

    public List<OutgoingEmail> Sent { get; } = [];

    public void Enqueue(SendResult result) => _script.Enqueue(result);

    public void EnqueueException(Exception exception) => _script.Enqueue(exception);

    public Task<SendResult> SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
    {
        Sent.Add(email);

        if (_script.Count == 0)
        {
            return Task.FromResult(SendResult.Success($"provider-{Sent.Count}"));
        }

        return _script.Dequeue() switch
        {
            Exception exception => Task.FromException<SendResult>(exception),
            SendResult result => Task.FromResult(result),
            _ => throw new InvalidOperationException("The script holds only results and exceptions."),
        };
    }
}

/// <summary>Applies the same rules as the real reader to the in-memory bookings and committed messages.</summary>
internal sealed class InMemoryReminderCandidateReader(InMemoryStore store) : IReminderCandidateReader
{
    public Task<IReadOnlyList<ReminderCandidate>> FindDueAsync(ReminderQuery query, CancellationToken cancellationToken)
    {
        var found = store.Bookings
            .Where(b => b.Status == BookingStatus.Confirmed
                        && b.StartsAtUtc > query.StartsAfterUtc
                        && b.StartsAtUtc <= query.StartsAtOrBeforeUtc
                        && b.UpdatedAtUtc <= query.UpdatedAtOrBeforeUtc)
            .Where(b => !store.Messages.Any(m =>
                m.BookingId == b.Id && m.Kind == NotificationKind.Reminder && m.StartsAtUtc == b.StartsAtUtc))
            .OrderBy(b => b.StartsAtUtc)
            .ThenBy(b => b.Id)
            .Take(query.Limit)
            .Select(b => new ReminderCandidate(b.Id, b.StartsAtUtc))
            .ToList();

        return Task.FromResult<IReadOnlyList<ReminderCandidate>>(found);
    }
}


