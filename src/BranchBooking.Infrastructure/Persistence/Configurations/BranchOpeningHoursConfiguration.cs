using BranchBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BranchBooking.Infrastructure.Persistence.Configurations;

public sealed class BranchOpeningHoursConfiguration : IEntityTypeConfiguration<BranchOpeningHours>
{
    public void Configure(EntityTypeBuilder<BranchOpeningHours> builder)
    {
        builder.ToTable(
            "branch_opening_hours",
            table => table.HasCheckConstraint("ck_branch_opening_hours_closes_after_opens", "closes_at > opens_at"));

        builder.HasKey(h => new { h.BranchId, h.DayOfWeek }).HasName("pk_branch_opening_hours");

        builder.Property(h => h.BranchId).HasColumnName("branch_id");
        builder.Property(h => h.DayOfWeek).HasColumnName("day_of_week");
        builder.Property(h => h.OpensAt).HasColumnName("opens_at");
        builder.Property(h => h.ClosesAt).HasColumnName("closes_at");
    }
}
