using BranchBooking.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace BranchBooking.Infrastructure.Notifications;

/// <summary>
/// The default channel. Sends nothing: it reports success so the dispatcher records the message and keeps its
/// rendered content for the demo. Logs the event without the address, the subject or the body.
/// </summary>
public sealed class SimulatedEmailSender(ILogger<SimulatedEmailSender> logger) : INotificationSender
{
    public string Channel => NotificationOptions.SimulatedChannel;

    public bool RetainsContent => true;

    public Task<SendResult> SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
    {
        logger.LogInformation("Simulated email {MessageId} \"sent\"", email.MessageId);
        return Task.FromResult(SendResult.Success($"sim-{email.MessageId:N}"));
    }
}
