using BranchBooking.Domain.Exceptions;
using BranchBooking.Domain.ValueObjects;
using Xunit;

namespace BranchBooking.UnitTests.Domain;

public class CustomerContactTests
{
    [Fact]
    public void Create_trims_and_normalises_values()
    {
        var contact = CustomerContact.Create("  Thandi Nkosi ", "  Thandi@Example.COM ", "082 123-4567");

        Assert.Equal("Thandi Nkosi", contact.FullName);
        Assert.Equal("thandi@example.com", contact.Email);
        Assert.Equal("0821234567", contact.Phone);
    }

    [Fact]
    public void Create_keeps_a_leading_plus_on_international_numbers()
    {
        var contact = CustomerContact.Create("Thandi Nkosi", "thandi@example.com", "+27 82 123 4567");

        Assert.Equal("+27821234567", contact.Phone);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A")]
    public void Create_rejects_a_missing_or_short_name(string? name)
    {
        Assert.Throws<DomainException>(() => CustomerContact.Create(name, "a@b.co", "0821234567"));
    }

    [Fact]
    public void Create_rejects_a_name_over_the_limit()
    {
        var name = new string('a', CustomerContact.FullNameMaxLength + 1);

        Assert.Throws<DomainException>(() => CustomerContact.Create(name, "a@b.co", "0821234567"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("@example.com")]
    [InlineData("a@")]
    [InlineData("a@@example.com")]
    [InlineData("a b@example.com")]
    [InlineData("a@example")]
    [InlineData("a@.example.com")]
    [InlineData("a@example..com")]
    public void Create_rejects_an_invalid_email(string? email)
    {
        Assert.Throws<DomainException>(() => CustomerContact.Create("Thandi Nkosi", email, "0821234567"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345678")]
    [InlineData("0821234567890123")]
    [InlineData("082abc4567")]
    [InlineData("++27821234567")]
    public void Create_rejects_an_invalid_phone(string? phone)
    {
        Assert.Throws<DomainException>(() => CustomerContact.Create("Thandi Nkosi", "a@b.co", phone));
    }
}
