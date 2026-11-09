using BranchBooking.Application.Bookings;
using Xunit;

namespace BranchBooking.UnitTests.Bookings;

public class ContactMaskingTests
{
    [Theory]
    [InlineData("thandi@example.com", "t*****@example.com")]
    [InlineData("a@example.com", "a*@example.com")]
    [InlineData("ab@example.com", "a*@example.com")]
    public void Email_keeps_the_first_letter_and_the_domain(string email, string expected)
    {
        Assert.Equal(expected, ContactMasking.Email(email));
    }

    [Theory]
    [InlineData("0821234567", "*******567")]
    [InlineData("+27821234567", "*********567")]
    [InlineData("123", "***")]
    public void Phone_keeps_only_the_last_three_characters(string phone, string expected)
    {
        Assert.Equal(expected, ContactMasking.Phone(phone));
    }

    [Fact]
    public void A_masked_value_never_contains_the_hidden_part()
    {
        Assert.DoesNotContain("hand", ContactMasking.Email("thandi@example.com"), StringComparison.Ordinal);
        Assert.DoesNotContain("123456", ContactMasking.Phone("0821234567"), StringComparison.Ordinal);
    }
}
