using BranchBooking.Domain.Entities;
using BranchBooking.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BranchBooking.Infrastructure.Persistence.Configurations;

public sealed class NotificationMessageConfiguration : IEntityTypeConfiguration<NotificationMessage>
{
    public void Configure(EntityTypeBuilder<NotificationMessage> builder)
    {
        builder.ToTable("notification_messages", table =>
        {
            table.HasCheckConstraint("ck_notification_messages_attempts_non_negative", "attempts >= 0");
            table.HasCheckConstraint("ck_notification_messages_sent_consistency", "(status = 'Sent') = (sent_at_utc IS NOT NULL)");
        });

        builder.HasKey(m => m.Id).HasName("pk_notification_messages");
        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(m => m.BookingId).HasColumnName("booking_id");
        builder.Property(m => m.Kind).HasColumnName("kind").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(m => m.DedupeKey).HasColumnName("dedupe_key").HasMaxLength(NotificationMessage.DedupeKeyMaxLength).IsRequired();
        builder.Property(m => m.StartsAtUtc).HasColumnName("starts_at_utc");
        builder.Property(m => m.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(m => m.Attempts).HasColumnName("attempts");
        builder.Property(m => m.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");
        builder.Property(m => m.LockedUntilUtc).HasColumnName("locked_until_utc");
        builder.Property(m => m.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(m => m.SentAtUtc).HasColumnName("sent_at_utc");
        builder.Property(m => m.Channel).HasColumnName("channel").HasMaxLength(NotificationMessage.ChannelMaxLength);
        builder.Property(m => m.ProviderMessageId).HasColumnName("provider_message_id").HasMaxLength(NotificationMessage.ProviderMessageIdMaxLength);
        builder.Property(m => m.Detail).HasColumnName("detail").HasMaxLength(NotificationMessage.DetailMaxLength);
        builder.Property(m => m.RenderedSubject).HasColumnName("rendered_subject").HasMaxLength(NotificationMessage.SubjectMaxLength);
        builder.Property(m => m.RenderedBody).HasColumnName("rendered_body").HasMaxLength(NotificationMessage.BodyMaxLength);

        builder.Property(m => m.Version).IsRowVersion();

        builder.HasOne(m => m.Booking)
            .WithMany()
            .HasForeignKey(m => m.BookingId)
            .HasConstraintName(DatabaseConstraints.NotificationBookingForeignKey)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.DedupeKey).IsUnique().HasDatabaseName(DatabaseConstraints.NotificationDedupeKeyUnique);

        // The Worker's "what is due" query touches only pending rows.
        builder.HasIndex(m => m.NextAttemptAtUtc)
            .HasFilter($"status = '{nameof(NotificationStatus.Pending)}'")
            .HasDatabaseName("ix_notification_messages_due");

        // Lists per booking and the reminder "already queued?" check.
        builder.HasIndex(m => new { m.BookingId, m.Kind }).HasDatabaseName("ix_notification_messages_booking_id");
    }
}
