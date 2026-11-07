using BranchBooking.Application.Abstractions;
using BranchBooking.Domain.Entities;
using BranchBooking.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace BranchBooking.Application.Notifications;

public sealed record DispatchSummary(int Claimed, int Sent, int Skipped, int Retried, int Failed);

/// <summary>
/// One pass of the dispatcher: claim due messages, then for each decide (skip, send, retry or fail) and save the
/// outcome. Delivery is at-least-once: if the process dies after the provider accepted a message but before the
/// outcome is saved, the message is sent again once its lease expires.
/// </summary>
public sealed class DispatchNotifications(
    INotificationQueue queue,
    IUnitOfWork unitOfWork,
    INotificationSender sender,
    NotificationContentBuilder content,
    NotificationPolicy policy,
    TimeProvider clock,
    ILogger<DispatchNotifications> logger)
{
    private enum Outcome
    {
        Sent,
        Skipped,
        Retried,
        Failed,
    }

    public async Task<DispatchSummary> ExecuteAsync(CancellationToken cancellationToken)
    {
        var batch = await queue.ClaimDueAsync(clock.GetUtcNow(), policy.BatchSize, policy.Lease, cancellationToken);

        int sent = 0, skipped = 0, retried = 0, failed = 0;
        foreach (var message in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var messageId = message.Id;
            var outcome = await ProcessAsync(message, cancellationToken);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                // The change tracker was cleared, so the rest of this batch can no longer be saved. Their leases
                // expire and they are claimed again on a later pass.
                logger.LogWarning(
                    "Notification {MessageId} changed while it was processed; the rest of the batch is left for a later pass",
                    messageId);
                break;
            }

            switch (outcome)
            {
                case Outcome.Sent: sent++; break;
                case Outcome.Skipped: skipped++; break;
                case Outcome.Retried: retried++; break;
                default: failed++; break;
            }
        }

        return new DispatchSummary(batch.Count, sent, skipped, retried, failed);
    }

    private async Task<Outcome> ProcessAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        var booking = message.Booking;

        var staleReason = StaleReason(message, booking, clock.GetUtcNow());
        if (staleReason is not null)
        {
            message.MarkSkipped(staleReason);
            logger.LogInformation("Notification {MessageId} ({Kind}) skipped: {Reason}", message.Id, message.Kind, staleReason);
            return Outcome.Skipped;
        }

        var rendered = content.Build(message, booking);
        var result = await SendAsync(message, booking, rendered, cancellationToken);
        var now = clock.GetUtcNow();

        if (result.Succeeded)
        {
            message.MarkSent(
                sender.Channel,
                result.ProviderMessageId,
                sender.RetainsContent ? rendered.Subject : null,
                sender.RetainsContent ? rendered.TextBody : null,
                now);
            logger.LogInformation(
                "Notification {MessageId} ({Kind}) sent through {Channel} for booking {BookingId}",
                message.Id, message.Kind, sender.Channel, booking.Id);
            return Outcome.Sent;
        }

        var error = result.Error ?? "unknown_error";
        message.RecordFailure(error, result.IsTransient, now, policy.MaxAttempts, policy.RetryDelay(message.Attempts + 1));

        if (message.Status == NotificationStatus.Failed)
        {
            logger.LogError(
                "Notification {MessageId} ({Kind}) failed for good after {Attempts} attempt(s): {Error}",
                message.Id, message.Kind, message.Attempts, error);
            return Outcome.Failed;
        }

        logger.LogWarning(
            "Notification {MessageId} ({Kind}) will be retried after attempt {Attempts}: {Error}",
            message.Id, message.Kind, message.Attempts, error);
        return Outcome.Retried;
    }

    private async Task<SendResult> SendAsync(
        NotificationMessage message, Booking booking, RenderedMessage rendered, CancellationToken cancellationToken)
    {
        var email = new OutgoingEmail(
            message.Id, booking.Customer.Email, booking.Customer.FullName, rendered.Subject, rendered.TextBody, rendered.HtmlBody);

        try
        {
            return await sender.SendAsync(email, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Only the exception type is kept: its message could contain an address or a provider response.
            return SendResult.Transient(ex.GetType().Name);
        }
    }

    /// <summary>Why a message should no longer be sent, or <c>null</c> when it is still true.</summary>
    private static string? StaleReason(NotificationMessage message, Booking booking, DateTimeOffset now)
    {
        if (booking.StartsAtUtc <= now)
        {
            return "appointment_passed";
        }

        if (message.Kind == NotificationKind.Cancellation)
        {
            return booking.Status == BookingStatus.Cancelled ? null : "booking_not_cancelled";
        }

        if (booking.Status != BookingStatus.Confirmed)
        {
            return "booking_not_confirmed";
        }

        return booking.StartsAtUtc == message.StartsAtUtc ? null : "superseded";
    }
}
