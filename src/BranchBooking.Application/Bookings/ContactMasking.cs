namespace BranchBooking.Application.Bookings;

/// <summary>Hides most of an email address or phone number while leaving enough to recognise it.</summary>
public static class ContactMasking
{
    /// <summary><c>thandi@example.com</c> becomes <c>t*****@example.com</c>.</summary>
    public static string Email(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0)
        {
            return new string('*', Math.Max(email.Length, 1));
        }

        return $"{email[0]}{new string('*', Math.Max(at - 1, 1))}{email[at..]}";
    }

    /// <summary><c>0821234567</c> becomes <c>*******567</c>.</summary>
    public static string Phone(string phone) =>
        phone.Length <= 3
            ? new string('*', phone.Length)
            : new string('*', phone.Length - 3) + phone[^3..];
}
