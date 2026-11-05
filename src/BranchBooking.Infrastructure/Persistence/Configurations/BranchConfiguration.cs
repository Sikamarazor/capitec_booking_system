using BranchBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BranchBooking.Infrastructure.Persistence.Configurations;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches", table =>
        {
            table.HasCheckConstraint(
                "ck_branches_slot_length_range",
                $"slot_length_minutes BETWEEN {Branch.MinSlotLengthMinutes} AND {Branch.MaxSlotLengthMinutes}");
            table.HasCheckConstraint(
                "ck_branches_slot_capacity_range",
                $"slot_capacity BETWEEN 1 AND {Branch.MaxSlotCapacity}");
        });

        builder.HasKey(b => b.Id).HasName("pk_branches");
        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(b => b.Code).HasColumnName("code").HasMaxLength(Branch.CodeMaxLength).IsRequired();
        builder.Property(b => b.Name).HasColumnName("name").HasMaxLength(Branch.NameMaxLength).IsRequired();
        builder.Property(b => b.AddressLine).HasColumnName("address_line").HasMaxLength(Branch.AddressMaxLength).IsRequired();
        builder.Property(b => b.City).HasColumnName("city").HasMaxLength(Branch.CityMaxLength).IsRequired();
        builder.Property(b => b.Province).HasColumnName("province").HasMaxLength(Branch.ProvinceMaxLength).IsRequired();
        builder.Property(b => b.TimeZoneId).HasColumnName("time_zone_id").HasMaxLength(Branch.TimeZoneIdMaxLength).IsRequired();
        builder.Property(b => b.SlotLengthMinutes).HasColumnName("slot_length_minutes");
        builder.Property(b => b.SlotCapacity).HasColumnName("slot_capacity");
        builder.Property(b => b.IsActive).HasColumnName("is_active");

        builder.HasIndex(b => b.Code).IsUnique().HasDatabaseName(DatabaseConstraints.BranchCodeUnique);

        // The collection is exposed read-only; EF fills the private _openingHours field.
        builder.HasMany(b => b.OpeningHours)
            .WithOne()
            .HasForeignKey(h => h.BranchId)
            .HasConstraintName("fk_branch_opening_hours_branches_branch_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(b => b.OpeningHours).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
