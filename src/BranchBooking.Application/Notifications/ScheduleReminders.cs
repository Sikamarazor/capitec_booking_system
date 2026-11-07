using BranchBooking.Application.Abstractions;
using BranchBooking.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BranchBooking.Application.Notifications;

public sealed record ReminderSummary(int Found, int Queued, int AlreadyQueued);

/// <summary>
/// One scan: find bookings that have entered the reminder window and queue a reminder for each. Each reminder is
/// saved on its own, so a duplicate found by the database (another Worker got there first) costs nothing else.
/// </summary>
public sealed class ScheduleReminders(
    IReminderCandidateReader reader,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    IOptions<ReminderOptions> options,
    TimeProvider clock,
    ILogger<ScheduleReminders> logger)
{
    public async Task<ReminderSummary> ExecuteAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return new ReminderSummary(0, 0, 0);
        }

        var now = clock.GetUtcNow();
        var query = new ReminderQuery(
            StartsAfterUtc: now + settings.MinimumLead,
            StartsAtOrBeforeUtc: now + settings.Lead,
            UpdatedAtOrBeforeUtc: now - settings.MinimumGap,
            Limit: settings.BatchSize);

        var candidates = await reader.FindDueAsync(query, cancellationToken);

        int queued = 0, alreadyQueued = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            notifications.Add(NotificationMessage.ForReminder(candidate.BookingId, candidate.StartsAtUtc, now));
            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                queued++;
            }
            catch (NotificationAlreadyQueuedException)
            {
                // Another scan queued it between our read and our save. That is the guarantee working.
                alreadyQueued++;
            }
        }

        if (candidates.Count > 0)
        {
            logger.LogInformation(
                "Reminder scan: {Found} due, {Queued} queued, {AlreadyQueued} already queued",
                candidates.Count, queued, alreadyQueued);
        }

        return new ReminderSummary(candidates.Count, queued, alreadyQueued);
    }
}
