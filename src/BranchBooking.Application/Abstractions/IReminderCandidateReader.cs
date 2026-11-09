namespace BranchBooking.Application.Abstractions;

/// <summary>A confirmed booking that should be reminded, with the start time the reminder is about.</summary>
public sealed record ReminderCandidate(Guid BookingId, DateTimeOffset StartsAtUtc);

/// <param name="StartsAfterUtc">Exclusive lower bound on the appointment start.</param>
/// <param name="StartsAtOrBeforeUtc">Inclusive upper bound on the appointment start.</param>
/// <param name="UpdatedAtOrBeforeUtc">Inclusive bound on when the booking last changed (the quiet gap after a confirmation).</param>
/// <param name="Limit">The most candidates to return.</param>
public sealed record ReminderQuery(
    DateTimeOffset StartsAfterUtc,
    DateTimeOffset StartsAtOrBeforeUtc,
    DateTimeOffset UpdatedAtOrBeforeUtc,
    int Limit);

public interface IReminderCandidateReader
{
    /// <summary>
    /// Confirmed bookings inside the window that have no reminder message for their current start,
    /// earliest appointment first. Read-only: nothing returned is tracked.
    /// </summary>
    Task<IReadOnlyList<ReminderCandidate>> FindDueAsync(ReminderQuery query, CancellationToken cancellationToken);
}
