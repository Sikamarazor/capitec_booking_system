using MimeKit;

namespace BranchBooking.Infrastructure.Notifications;

/// <summary>How the connection to the SMTP server is protected.</summary>
public enum SmtpSecurity
{
    /// <summary>No encryption. For a local mail catcher such as Mailpit only.</summary>
    None = 0,

    /// <summary>Plain connection upgraded with STARTTLS (usually port 587). The connection fails if the upgrade is refused.</summary>
    StartTls = 1,

    /// <summary>TLS from the first byte (usually port 465).</summary>
    SslOnConnect = 2,
}

/// <summary>
/// SMTP settings (<c>Notifications:Smtp</c> section). Only checked when the channel is <c>Smtp</c>.
/// <see cref="Password"/> is a secret: supply it through the environment or user-secrets, never a committed file.
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Notifications:Smtp";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;

    /// <summary>Leave empty for a server that needs no login (Mailpit).</summary>
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>The From address. A real server usually insists that you are allowed to send as it.</summary>
    public string SenderEmail { get; set; } = string.Empty;

    public string SenderName { get; set; } = "BranchBooking";

    /// <summary>Covers the whole send: connect, log in, transmit.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    public bool HasCredentials => !string.IsNullOrEmpty(Username);

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(Host)
        && Port is >= 1 and <= 65535
        && Enum.IsDefined(Security)
        && HasCredentials == !string.IsNullOrEmpty(Password)
        && (!HasCredentials || Security != SmtpSecurity.None)
        && MailboxAddress.TryParse(SenderEmail, out _)
        && !string.IsNullOrWhiteSpace(SenderName)
        && TimeoutSeconds is >= 1 and <= 60;
}
