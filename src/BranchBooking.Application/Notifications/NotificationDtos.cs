namespace BranchBooking.Application.Notifications;

/// <param name="Kind"><c>Confirmation</c>, <c>Cancellation</c>, <c>Reschedule</c> or <c>Reminder</c>.</param>
/// <param name="Status"><c>Pending</c>, <c>Sent</c>, <c>Failed</c> or <c>Skipped</c>.</param>
/// <param name="Channel">The channel that sent it; <c>null</c> until sent.</param>
/// <param name="Subject">Only for the simulated channel, once sent.</param>
/// <param name="Body">Only for the simulated channel, once sent.</param>
public sealed record NotificationMessageDto(
    string Kind,
    string Status,
    string? Channel,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SentAtUtc,
    string? Subject,
    string? Body);
