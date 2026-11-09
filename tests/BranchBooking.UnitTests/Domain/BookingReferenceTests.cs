using System.Text.RegularExpressions;
using BranchBooking.Domain.ValueObjects;
using Xunit;

namespace BranchBooking.UnitTests.Domain;

public partial class BookingReferenceTests
{
    [GeneratedRegex("^BK-[0-9A-HJKMNP-TV-Z]{5}-[0-9A-HJKMNP-TV-Z]{5}$")]
    private static partial Regex CanonicalFormat();

    [Fact]
    public void Generate_produces_the_canonical_format()
    {
        var reference = BookingReference.Generate();

        Assert.Matches(CanonicalFormat(), reference.Value);
        Assert.Equal(reference.Value, reference.ToString());
        Assert.True(reference.Value.Length <= BookingReference.MaxLength);
    }

    [Fact]
    public void Generate_does_not_repeat_across_ten_thousand_samples()
    {
        var seen = new HashSet<string>();

        for (var i = 0; i < 10_000; i++)
        {
            Assert.True(seen.Add(BookingReference.Generate().Value));
        }
    }

    [Theory]
    [InlineData("bk-abcde-fghjk", "BK-ABCDE-FGHJK")]
    [InlineData("  BK ABCDE FGHJK  ", "BK-ABCDE-FGHJK")]
    [InlineData("BKABCDEFGHJK", "BK-ABCDE-FGHJK")]
    [InlineData("BK-0O1IL-ABCDE", "BK-00111-ABCDE")]
    public void Parse_normalises_input(string input, string expected)
    {
        Assert.True(BookingReference.TryParse(input, out var reference));
        Assert.Equal(expected, reference.Value);
    }

    [Fact]
    public void Parse_round_trips_a_generated_reference()
    {
        var generated = BookingReference.Generate();

        Assert.True(BookingReference.TryParse(generated.Value.ToLowerInvariant(), out var parsed));
        Assert.Equal(generated, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("BK-12345")]
    [InlineData("XX-ABCDE-FGHJK")]
    [InlineData("ABCDE-FGHJK")]
    [InlineData("BK-ABCDE-FGHJU")]
    [InlineData("BK-ABCDE-FGHJKM")]
    [InlineData("BK-ABC!E-FGHJK")]
    public void TryParse_rejects_invalid_input(string? input)
    {
        Assert.False(BookingReference.TryParse(input, out var reference));
        Assert.Equal(default(BookingReference), reference);
    }

    [Fact]
    public void Parse_throws_FormatException_for_invalid_input()
    {
        Assert.Throws<FormatException>(() => BookingReference.Parse("nope"));
    }

    [Fact]
    public void Default_reference_is_not_equal_to_a_generated_one()
    {
        Assert.NotEqual(default(BookingReference), BookingReference.Generate());
    }
}
