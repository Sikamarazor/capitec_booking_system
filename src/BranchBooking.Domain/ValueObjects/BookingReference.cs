using System.Security.Cryptography;

namespace BranchBooking.Domain.ValueObjects;

public readonly record struct BookingReference
{
    public const string Prefix = "BK";
    public const int MaxLength = 14; // "BK-" + 5 + "-" + 5

    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int CodeLength = 10;

    private BookingReference(string value) => Value = value;

    /// <summary>Canonical text form, or <c>null</c> for <c>default</c>.</summary>
    public string Value { get; }

    public static BookingReference Generate()
    {
        Span<char> code = stackalloc char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
        {
            code[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new BookingReference(Format(code));
    }

    /// <summary>
    /// Accepts any casing, with or without hyphens and spaces, and maps look-alike characters
    /// (<c>O</c> to <c>0</c>, <c>I</c> and <c>L</c> to <c>1</c>). The <c>BK</c> prefix is required.
    /// </summary>
    public static bool TryParse(string? input, out BookingReference reference)
    {
        reference = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var text = input.AsSpan().Trim();
        if (!text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        text = text[Prefix.Length..];

        Span<char> code = stackalloc char[CodeLength];
        var count = 0;
        foreach (var raw in text)
        {
            if (raw is '-' or ' ')
            {
                continue;
            }

            var c = char.ToUpperInvariant(raw);
            c = c switch
            {
                'O' => '0',
                'I' or 'L' => '1',
                _ => c,
            };

            if (count == CodeLength || Alphabet.IndexOf(c) < 0)
            {
                return false;
            }

            code[count++] = c;
        }

        if (count != CodeLength)
        {
            return false;
        }

        reference = new BookingReference(Format(code));
        return true;
    }

    public static BookingReference Parse(string input) =>
        TryParse(input, out var reference)
            ? reference
            : throw new FormatException("Not a valid booking reference.");

    public override string ToString() => Value ?? string.Empty;

    private static string Format(ReadOnlySpan<char> code) => $"{Prefix}-{code[..5]}-{code[5..]}";
}
