using BranchBooking.Domain.Entities;
using BranchBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace BranchBooking.UnitTests.Persistence;

public class ModelConfigurationTests
{
    private static IModel BuildModel()
    {
        // The connection string is never used: building the model does not open a connection.
        var options = new DbContextOptionsBuilder<BranchBookingDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=model;Password=model")
            .Options;

        using var context = new BranchBookingDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }

    [Fact]
    public void Tables_use_the_expected_snake_case_names()
    {
        var tables = BuildModel().GetEntityTypes().Select(e => e.GetTableName()).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(new[] { "appointment_types", "bookings", "branch_opening_hours", "branches", "notification_messages" }, tables);
    }

    [Fact]
    public void Confirmed_slot_index_is_unique_filtered_and_covers_branch_start_and_position()
    {
        var booking = BuildModel().FindEntityType(typeof(Booking))!;

        var index = Assert.Single(booking.GetIndexes(), i => i.GetDatabaseName() == DatabaseConstraints.ConfirmedSlotUnique);

        Assert.True(index.IsUnique);
        Assert.Equal("status = 'Confirmed'", index.GetFilter());
        Assert.Equal(
            [nameof(Booking.BranchId), nameof(Booking.StartsAtUtc), nameof(Booking.SlotNumber)],
            index.Properties.Select(p => p.Name));
    }

    [Fact]
    public void Booking_reference_has_a_unique_index()
    {
        var booking = BuildModel().FindEntityType(typeof(Booking))!;

        var index = Assert.Single(booking.GetIndexes(), i => i.GetDatabaseName() == DatabaseConstraints.BookingReferenceUnique);

        Assert.True(index.IsUnique);
        Assert.Null(index.GetFilter());
    }

    [Fact]
    public void Booking_version_is_the_xmin_concurrency_token()
    {
        var booking = BuildModel().FindEntityType(typeof(Booking))!;
        var version = booking.FindProperty(nameof(Booking.Version))!;

        Assert.True(version.IsConcurrencyToken);
        Assert.Equal("xmin", version.GetColumnName());
    }

    [Fact]
    public void Bookings_have_the_expected_check_constraints()
    {
        var booking = BuildModel().FindEntityType(typeof(Booking))!;

        var names = booking.GetCheckConstraints().Select(c => c.Name).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(
            new[]
            {
                "ck_bookings_cancelled_consistency",
                "ck_bookings_ends_after_starts",
                "ck_bookings_reschedule_count_non_negative",
                "ck_bookings_slot_number_positive",
            },
            names);
    }

    [Fact]
    public void Branch_and_appointment_type_codes_are_unique()
    {
        var model = BuildModel();

        var branchIndex = Assert.Single(
            model.FindEntityType(typeof(Branch))!.GetIndexes(), i => i.GetDatabaseName() == DatabaseConstraints.BranchCodeUnique);
        var typeIndex = Assert.Single(
            model.FindEntityType(typeof(AppointmentType))!.GetIndexes(), i => i.GetDatabaseName() == DatabaseConstraints.AppointmentTypeCodeUnique);

        Assert.True(branchIndex.IsUnique);
        Assert.True(typeIndex.IsUnique);
    }
}
