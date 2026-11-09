using BranchBooking.Domain.Exceptions;

namespace BranchBooking.Domain.ValueObjects;

/// <summary>Who the appointment is for and how to reach them. Values are trimmed and normalised.</summary>
public sealed record CustomerContact
{
    public const int FullNameMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const int PhoneMaxLength = 20;

    private CustomerContact()
    {
        // Required by EF Core for materialisation.
    }

    public string FullName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string Phone { get; private set; } = string.Empty;

    public static CustomerContact Create(string? fullName, string? email, string? phone)
    {
        var name = (fullName ?? string.Empty).Trim();
        if (name.Length is < 2 or > FullNameMaxLength)
        {
            throw new DomainException($"Full name must be between 2 and {FullNameMaxLength} characters.");
        }

        var normalisedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (!IsValidEmail(normalisedEmail))
        {
            throw new DomainException("Email address is not valid.");
        }

        var normalisedPhone = NormalisePhone(phone);
        if (normalisedPhone is null)
        {
            throw new DomainException("Phone number must have 9 to 15 digits, with an optional leading +.");
        }

        return new CustomerContact { FullName = name, Email = normalisedEmail, Phone = normalisedPhone };
    }

    private static bool IsValidEmail(string email)
    {
        if (email.Length is 0 or > EmailMaxLength || email.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var at = email.IndexOf('@');
        if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1)
        {
            return false;
        }

        var domain = email[(at + 1)..];
        return domain.Contains('.')
            && !domain.StartsWith('.')
            && !domain.EndsWith('.')
            && !domain.Contains("..", StringComparison.Ordinal);
    }

    /// <summary>Strips spaces, hyphens and brackets; returns <c>null</c> if the result is not a plausible number.</summary>
    private static string? NormalisePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var cleaned = new string(phone.Trim().Where(c => c is not (' ' or '-' or '(' or ')')).ToArray());
        var digits = cleaned.StartsWith('+') ? cleaned[1..] : cleaned;

        if (digits.Length is < 9 or > 15 || !digits.All(char.IsAsciiDigit))
        {
            return null;
        }

        return cleaned;
    }
}
