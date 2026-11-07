using System.Net.Sockets;
using BranchBooking.Application.Notifications;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BranchBooking.Infrastructure.Notifications;

/// <summary>
/// Builds one email (text and HTML versions) and sends it through <see cref="ISmtpTransport"/>.
/// Failures become short codes (<c>smtp_{reply code}</c>, <c>smtp_auth_failed</c>, <c>timeout</c>, ...); the server's reply
/// text and exception messages are never kept, because they can echo the recipient.
/// Retry rule: 4xx replies, protocol errors, timeouts and network errors are transient; every other failure is permanent.
/// An exception of any other type is not caught here; the dispatcher records its type name and treats it as transient.
/// </summary>
public sealed class SmtpEmailSender(ISmtpTransport transport, IOptions<SmtpOptions> options) : INotificationSender
{
    public string Channel => NotificationOptions.SmtpChannel;

    public bool RetainsContent => false;

    public async Task<SendResult> SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (!MailboxAddress.TryParse(settings.SenderEmail, out var from))
        {
            return SendResult.Permanent("sender_invalid");
        }

        if (!MailboxAddress.TryParse(email.ToEmail, out var to))
        {
            return SendResult.Permanent("invalid_recipient");
        }

        var messageId = $"{email.MessageId:N}@{DomainOf(from.Address)}";
        var message = new MimeMessage { MessageId = messageId, Subject = Clean(email.Subject) };
        message.From.Add(new MailboxAddress(Clean(settings.SenderName), from.Address));
        message.To.Add(new MailboxAddress(Clean(email.ToName), to.Address));
        message.Body = new BodyBuilder { TextBody = email.TextBody, HtmlBody = email.HtmlBody }.ToMessageBody();

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));

        try
        {
            await transport.SendAsync(message, deadline.Token);
            return SendResult.Success(messageId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return SendResult.Transient("timeout");
        }
        catch (TimeoutException)
        {
            return SendResult.Transient("timeout");
        }
        catch (SmtpCommandException failure)
        {
            var status = (int)failure.StatusCode;
            return status switch
            {
                >= 400 and < 500 => SendResult.Transient($"smtp_{status}"),
                >= 500 and < 600 => SendResult.Permanent($"smtp_{status}"),
                _ => SendResult.Permanent("smtp_error"),
            };
        }
        catch (MailKit.Security.AuthenticationException)
        {
            return SendResult.Permanent("smtp_auth_failed");
        }
        catch (SslHandshakeException)
        {
            return SendResult.Permanent("smtp_tls_failed");
        }
        catch (ProtocolException)
        {
            return SendResult.Transient("smtp_protocol_error");
        }
        catch (Exception failure) when (failure is IOException or SocketException)
        {
            return SendResult.Transient("network_error");
        }
    }

    private static string DomainOf(string address) => address[(address.LastIndexOf('@') + 1)..];

    /// <summary>Removes line breaks so a name or subject can never add a header.</summary>
    private static string Clean(string value) => value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
