using BranchBooking.Domain.Enums;
using BranchBooking.Domain.Exceptions;

namespace BranchBooking.Domain.Entities;

/// <summary>
/// A notification waiting to be sent, or the record of one that was. It is created in the same transaction as the
/// booking change that caused it (an outbox), and sent later by the Worker.
/// </summary>
public sealed class NotificationMessage
{
    public const int DedupeKeyMaxLength = 100;
    public const int ChannelMaxLength = 20;
    public const int DetailMaxLength = 200;
    public const int ProviderMessageIdMaxLength = 200;
    public const int SubjectMaxLength = 200;
    public const int BodyMaxLength = 4000;

    private NotificationMessage()
    {
        // Required by EF Core.
    }

    public Guid Id { get; private set; }

    public Guid BookingId { get; private set; }

    public NotificationKind Kind { get; private set; }

    /// <summary>Unique per notification, so the same one can never be queued twice.</summary>
    public string DedupeKey { get; private set; } = null!;

    /// <summary>The appointment start this message was written for; a different current start makes it stale.</summary>
    public DateTimeOffset StartsAtUtc { get; private set; }

    public NotificationStatus Status { get; private set; }

    /// <summary>Delivery attempts made so far (a skipped message has none).</summary>
    public int Attempts { get; private set; }

    public DateTimeOffset NextAttemptAtUtc { get; private set; }

    /// <summary>Set by the claim query while a Worker holds the message; it expires if the Worker dies.</summary>
    public DateTimeOffset? LockedUntilUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? SentAtUtc { get; private set; }

    public string? Channel { get; private set; }

    public string? ProviderMessageId { get; private set; }

    /// <summary>A short code: why it was skipped or why the last attempt failed. Never a provider response or personal data.</summary>
    public string? Detail { get; private set; }

    /// <summary>Kept only by the simulated channel so the demo can show what was "emailed".</summary>
    public string? RenderedSubject { get; private set; }

    public string? RenderedBody { get; private set; }

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>). Maintained by the database.</summary>
    public uint Version { get; private set; }

    public Booking Booking { get; private set; } = null!;

    public static NotificationMessage ForConfirmation(Booking booking, DateTimeOffset nowUtc) =>
        Create(NotificationKind.Confirmation, booking.Id, booking.StartsAtUtc, $"confirmation:{booking.Id:N}", nowUtc);

    public static NotificationMessage ForCancellation(Booking booking, DateTimeOffset nowUtc) =>
        Create(NotificationKind.Cancellation, booking.Id, booking.StartsAtUtc, $"cancellation:{booking.Id:N}", nowUtc);

    /// <summary>Call after <see cref="Booking.Reschedule"/>: the key uses the new change count.</summary>
    public static NotificationMessage ForReschedule(Booking booking, DateTimeOffset nowUtc) =>
        Create(
            NotificationKind.Reschedule,
            booking.Id,
            booking.StartsAtUtc,
            $"reschedule:{booking.Id:N}:{booking.RescheduleCount}",
            nowUtc);

    public static NotificationMessage ForReminder(Guid bookingId, DateTimeOffset startsAtUtc, DateTimeOffset nowUtc) =>
        Create(
            NotificationKind.Reminder,
            bookingId,
            startsAtUtc,
            $"reminder:{bookingId:N}:{startsAtUtc.ToUnixTimeSeconds()}",
            nowUtc);

    public void MarkSent(
        string channel,
        string? providerMessageId,
        string? renderedSubject,
        string? renderedBody,
        DateTimeOffset nowUtc)
    {
        EnsurePending("marked as sent");

        if (string.IsNullOrWhiteSpace(channel) || channel.Length > ChannelMaxLength)
        {
            throw new DomainException($"The channel is required and at most {ChannelMaxLength} characters.");
        }

        Attempts++;
        Status = NotificationStatus.Sent;
        SentAtUtc = nowUtc.ToUniversalTime();
        Channel = channel;
        ProviderMessageId = Truncate(providerMessageId, ProviderMessageIdMaxLength);
        RenderedSubject = Truncate(renderedSubject, SubjectMaxLength);
        RenderedBody = Truncate(renderedBody, BodyMaxLength);
        Detail = null;
        LockedUntilUtc = null;
    }

    public void MarkSkipped(string reason)
    {
        EnsurePending("skipped");

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("A reason is required.");
        }

        Status = NotificationStatus.Skipped;
        Detail = Truncate(reason, DetailMaxLength);
        LockedUntilUtc = null;
    }

    /// <summary>
    /// Records a failed attempt. A transient failure keeps the message pending for another try at
    /// <paramref name="nowUtc"/> plus <paramref name="retryDelay"/>; a permanent one, or the last allowed attempt,
    /// ends it as <see cref="NotificationStatus.Failed"/>.
    /// </summary>
    public void RecordFailure(string error, bool isTransient, DateTimeOffset nowUtc, int maxAttempts, TimeSpan retryDelay)
    {
        EnsurePending("recorded as failed");

        if (string.IsNullOrWhiteSpace(error))
        {
            throw new DomainException("An error code is required.");
        }

        if (maxAttempts < 1)
        {
            throw new DomainException("At least one attempt must be allowed.");
        }

        if (retryDelay < TimeSpan.Zero)
        {
            throw new DomainException("The retry delay cannot be negative.");
        }

        Attempts++;
        Detail = Truncate(error, DetailMaxLength);
        LockedUntilUtc = null;

        if (!isTransient || Attempts >= maxAttempts)
        {
            Status = NotificationStatus.Failed;
            return;
        }

        NextAttemptAtUtc = nowUtc.ToUniversalTime() + retryDelay;
    }

    private static NotificationMessage Create(
        NotificationKind kind, Guid bookingId, DateTimeOffset startsAtUtc, string dedupeKey, DateTimeOffset nowUtc)
    {
        if (bookingId == Guid.Empty)
        {
            throw new DomainException("A booking is required.");
        }

        return new NotificationMessage
        {
            Id = Guid.CreateVersion7(),
            BookingId = bookingId,
            Kind = kind,
            DedupeKey = dedupeKey,
            StartsAtUtc = startsAtUtc.ToUniversalTime(),
            Status = NotificationStatus.Pending,
            Attempts = 0,
            NextAttemptAtUtc = nowUtc.ToUniversalTime(),
            CreatedAtUtc = nowUtc.ToUniversalTime(),
        };
    }

    private void EnsurePending(string action)
    {
        if (Status != NotificationStatus.Pending)
        {
            throw new DomainException($"Only a pending notification can be {action}.");
        }
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
