using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BranchBooking.Infrastructure.Notifications;

/// <summary>
/// The only class that opens a socket: connect, log in when credentials are set, send, disconnect. One connection per
/// message. Not unit tested (it needs a server); it is covered by the Mailpit check in the verification steps.
/// </summary>
public sealed class MailKitSmtpTransport(IOptions<SmtpOptions> options) : ISmtpTransport
{
    public async Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        using var client = new SmtpClient { Timeout = settings.TimeoutSeconds * 1000 };

        await client.ConnectAsync(settings.Host, settings.Port, ToSocketOptions(settings.Security), cancellationToken);

        if (settings.HasCredentials)
        {
            await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);

        try
        {
            await client.DisconnectAsync(quit: true, CancellationToken.None);
        }
        catch (Exception)
        {
            // The server has already accepted the message. A failure to say goodbye must not turn into a retry,
            // because that would send the email twice.
        }
    }

    private static SecureSocketOptions ToSocketOptions(SmtpSecurity security) => security switch
    {
        SmtpSecurity.None => SecureSocketOptions.None,
        SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
        SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        _ => throw new ArgumentOutOfRangeException(nameof(security), security, "Unknown SMTP security setting."),
    };
}
