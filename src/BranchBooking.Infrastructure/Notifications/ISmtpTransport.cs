using MimeKit;

namespace BranchBooking.Infrastructure.Notifications;

/// <summary>Hands one finished message to an SMTP server. The seam that lets tests run without a network.</summary>
public interface ISmtpTransport
{
    /// <summary>Returns when the server has accepted the message; throws when it has not.</summary>
    Task SendAsync(MimeMessage message, CancellationToken cancellationToken);
}
