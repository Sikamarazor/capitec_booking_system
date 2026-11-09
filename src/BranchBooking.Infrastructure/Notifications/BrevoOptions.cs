namespace BranchBooking.Infrastructure.Notifications;

/// <summary>
/// Brevo settings (<c>Notifications:Brevo</c> section). Only checked when the channel is <c>Brevo</c>.
/// <see cref="ApiKey"/> is a secret: supply it through the environment or user-secrets, never a committed file.
/// </summary>
public sealed class BrevoOptions
{
    public const string SectionName = "Notifications:Brevo";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>An address verified as a sender in the Brevo account.</summary>
    public string SenderEmail { get; set; } = string.Empty;

    public string SenderName { get; set; } = "BranchBooking";

    /// <summary>Must end with a slash so relative paths resolve beneath it.</summary>
    public string BaseUrl { get; set; } = "https://api.brevo.com/v3/";

    public int TimeoutSeconds { get; set; } = 10;

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(SenderEmail)
        && SenderEmail.Contains('@', StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(SenderName)
        && BaseUrl.EndsWith('/')
        && Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri)
        && baseUri.Scheme == Uri.UriSchemeHttps
        && TimeoutSeconds is >= 1 and <= 60;
}
