namespace BranchBooking.Application.Notifications;

/// <summary>One email, ready to send.</summary>
/// <param name="MessageId">The outbox row id, usable as an idempotency or tracking key.</param>
public sealed record OutgoingEmail(
    Guid MessageId, string ToEmail, string ToName, string Subject, string TextBody, string HtmlBody);

/// <summary>What a sender reports. <see cref="Error"/> is a short code, never a provider response.</summary>
public sealed record SendResult(bool Succeeded, bool IsTransient, string? ProviderMessageId, string? Error)
{
    public static SendResult Success(string? providerMessageId) => new(true, false, providerMessageId, null);

    /// <summary>Worth trying again later (rate limit, outage, timeout).</summary>
    public static SendResult Transient(string error) => new(false, true, null, error);

    /// <summary>Retrying will not help (rejected request, bad credentials).</summary>
    public static SendResult Permanent(string error) => new(false, false, null, error);
}

public interface INotificationSender
{
    /// <summary>Stored on the message, for example <c>Simulated</c> or <c>Brevo</c>.</summary>
    string Channel { get; }

    /// <summary>True when the rendered subject and body should be kept on the message row (simulated channel only).</summary>
    bool RetainsContent { get; }

    Task<SendResult> SendAsync(OutgoingEmail email, CancellationToken cancellationToken);
}
