using BranchBooking.Domain.Entities;
using BranchBooking.Domain.Enums;
using BranchBooking.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BranchBooking.Infrastructure.Persistence.Configurations;

public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings", table =>
        {
            table.HasCheckConstraint("ck_bookings_slot_number_positive", "slot_number >= 1");
            table.HasCheckConstraint("ck_bookings_ends_after_starts", "ends_at_utc > starts_at_utc");
            table.HasCheckConstraint("ck_bookings_reschedule_count_non_negative", "reschedule_count >= 0");
            table.HasCheckConstraint(
                "ck_bookings_cancelled_consistency",
                "(status = 'Cancelled') = (cancelled_at_utc IS NOT NULL)");
        });

        builder.HasKey(b => b.Id).HasName("pk_bookings");
        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(b => b.Reference)
            .HasColumnName("reference")
            .HasMaxLength(BookingReference.MaxLength)
            .HasConversion(reference => reference.Value, text => BookingReference.Parse(text))
            .IsRequired();

        builder.Property(b => b.BranchId).HasColumnName("branch_id");
        builder.Property(b => b.AppointmentTypeId).HasColumnName("appointment_type_id");
        builder.Property(b => b.StartsAtUtc).HasColumnName("starts_at_utc");
        builder.Property(b => b.EndsAtUtc).HasColumnName("ends_at_utc");
        builder.Property(b => b.SlotNumber).HasColumnName("slot_number");

        // Stored as readable text so the partial index below can filter on it.
        builder.Property(b => b.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(b => b.RescheduleCount).HasColumnName("reschedule_count");
        builder.Property(b => b.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(b => b.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(b => b.CancelledAtUtc).HasColumnName("cancelled_at_utc");
        builder.Property(b => b.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(Booking.CancellationReasonMaxLength);

        // Optimistic concurrency through PostgreSQL's xmin system column. The provider maps a uint
        // row version to xmin, so no column is created by the migration.
        builder.Property(b => b.Version).IsRowVersion();

        builder.ComplexProperty(b => b.Customer, customer =>
        {
            customer.Property(c => c.FullName).HasColumnName("customer_full_name").HasMaxLength(CustomerContact.FullNameMaxLength).IsRequired();
            customer.Property(c => c.Email).HasColumnName("customer_email").HasMaxLength(CustomerContact.EmailMaxLength).IsRequired();
            customer.Property(c => c.Phone).HasColumnName("customer_phone").HasMaxLength(CustomerContact.PhoneMaxLength).IsRequired();
        });

        builder.Ignore(b => b.Slot);

        builder.HasOne(b => b.Branch)
            .WithMany()
            .HasForeignKey(b => b.BranchId)
            .HasConstraintName(DatabaseConstraints.BookingBranchForeignKey)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.AppointmentType)
            .WithMany()
            .HasForeignKey(b => b.AppointmentTypeId)
            .HasConstraintName("fk_bookings_appointment_types_appointment_type_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.Reference).IsUnique().HasDatabaseName(DatabaseConstraints.BookingReferenceUnique);

        // The double-booking guard: at most one CONFIRMED booking per position. Cancelling a booking
        // (status leaves 'Confirmed') releases its position without deleting any history.
        builder.HasIndex(b => new { b.BranchId, b.StartsAtUtc, b.SlotNumber })
            .IsUnique()
            .HasFilter($"status = '{nameof(BookingStatus.Confirmed)}'")
            .HasDatabaseName(DatabaseConstraints.ConfirmedSlotUnique);

        builder.HasIndex(b => b.AppointmentTypeId).HasDatabaseName("ix_bookings_appointment_type_id");

                // The reminder scan looks only at confirmed bookings by start time.
        builder.HasIndex(b => b.StartsAtUtc)
            .HasDatabaseName(DatabaseConstraints.ConfirmedStartIndex)
            .HasFilter($"status = '{nameof(BookingStatus.Confirmed)}'");

    }
}
