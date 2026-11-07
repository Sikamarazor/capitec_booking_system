namespace BranchBooking.Application.Notifications;

/// <summary>Settings for sending (<c>Notifications</c> section). Defaults are planner choices.</summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";
    public const string SimulatedChannel = "Simulated";
    public const string SmtpChannel = "Smtp";

    /// <summary><c>Simulated</c> (default, sends nothing) or <c>Smtp</c> (email through an SMTP server).</summary>
    public string Channel { get; set; } = SimulatedChannel;

    /// <summary>Named in the message footer. Deliberately not a real institution's name by default.</summary>
    public string BrandName { get; set; } = "BranchBooking";

    /// <summary>Where the web app lives, for the link in messages.</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>The web app's page for viewing and changing a booking.</summary>
    public string ManagePath { get; set; } = "/manage";

    /// <summary>Messages claimed per pass.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>Seconds between passes.</summary>
    public int PollSeconds { get; set; } = 10;

    /// <summary>Attempts before a message is marked failed.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Delay after the first failure; it doubles after each further failure.</summary>
    public int RetryBaseSeconds { get; set; } = 30;

    public int RetryMaxSeconds { get; set; } = 3600;

    /// <summary>How long a claimed message is reserved. Must outlast a whole batch of sends.</summary>
    public int LeaseSeconds { get; set; } = 300;

    public bool UsesSmtp => string.Equals(Channel, SmtpChannel, StringComparison.OrdinalIgnoreCase);

    public bool IsValid() =>
        (string.Equals(Channel, SimulatedChannel, StringComparison.OrdinalIgnoreCase) || UsesSmtp)
        && !string.IsNullOrWhiteSpace(BrandName)
        && BrandName.Length <= 70
        && Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var baseUri)
        && baseUri.Scheme is "http" or "https"
        && ManagePath.StartsWith('/')
        && BatchSize is >= 1 and <= 500
        && PollSeconds is >= 1 and <= 3600
        && MaxAttempts is >= 1 and <= 20
        && RetryBaseSeconds is >= 1 and <= 3600;
        // && RetryMaxSeconds >= RetryBaseSeconds and <= 86_400
        // && LeaseSeconds is >= 30 and <= 3600;
}
