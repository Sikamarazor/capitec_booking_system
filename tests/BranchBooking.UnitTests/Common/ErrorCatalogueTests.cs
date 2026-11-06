using System.Reflection;
using System.Text.RegularExpressions;
using BranchBooking.Application.Common;

namespace BranchBooking.UnitTests.Common;

public partial class ErrorCatalogueTests
{
    /// <summary>Keep in step with the table in docs/api/README.md.</summary>
    private static readonly string[] ExpectedCodes =
    [
        "appointment_type_not_found",
        "booking_not_active",
        "booking_not_found",
        "branch_not_found",
        "change_window_closed",
        "concurrent_change",
        "date_outside_booking_window",
        "invalid_cancellation_reason",
        "invalid_customer_details",
        "reschedule_limit_reached",
        "same_slot",
        "slot_not_available",
        "slot_not_offered",
        "slot_too_soon",
    ];

    [GeneratedRegex("^[a-z]+(_[a-z]+)*$")]
    private static partial Regex SnakeCase();

    private static List<Error> AllErrors()
    {
        var errors = new List<Error>();

        foreach (var field in typeof(Errors).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(Error)))
        {
            errors.Add((Error)field.GetValue(null)!);
        }

        foreach (var method in typeof(Errors).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.ReturnType == typeof(Error)))
        {
            var arguments = method.GetParameters().Select(p => p.ParameterType == typeof(int) ? (object)5 : "reason").ToArray();
            errors.Add((Error)method.Invoke(null, arguments)!);
        }

        return errors;
    }

    [Fact]
    public void The_catalogue_matches_the_documented_codes_exactly()
    {
        Assert.Equal(ExpectedCodes, AllErrors().Select(e => e.Code).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Codes_are_unique_lower_snake_case()
    {
        var codes = AllErrors().Select(e => e.Code).ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.All(codes, code => Assert.Matches(SnakeCase(), code));
    }

    [Fact]
    public void Messages_are_complete_sentences_a_customer_can_read()
    {
        Assert.All(AllErrors(), error =>
        {
            Assert.False(string.IsNullOrWhiteSpace(error.Message));
            Assert.EndsWith(".", error.Message, StringComparison.Ordinal);
            Assert.True(Enum.IsDefined(error.Type));
        });
    }
}
