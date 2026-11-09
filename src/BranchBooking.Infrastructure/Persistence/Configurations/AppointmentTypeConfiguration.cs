using BranchBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BranchBooking.Infrastructure.Persistence.Configurations;

public sealed class AppointmentTypeConfiguration : IEntityTypeConfiguration<AppointmentType>
{
    public void Configure(EntityTypeBuilder<AppointmentType> builder)
    {
        builder.ToTable("appointment_types");

        builder.HasKey(t => t.Id).HasName("pk_appointment_types");
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(t => t.Code).HasColumnName("code").HasMaxLength(AppointmentType.CodeMaxLength).IsRequired();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(AppointmentType.NameMaxLength).IsRequired();
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(AppointmentType.TextMaxLength).IsRequired();
        builder.Property(t => t.WhatToBring).HasColumnName("what_to_bring").HasMaxLength(AppointmentType.TextMaxLength).IsRequired();
        builder.Property(t => t.SortOrder).HasColumnName("sort_order");
        builder.Property(t => t.IsActive).HasColumnName("is_active");

        builder.HasIndex(t => t.Code).IsUnique().HasDatabaseName(DatabaseConstraints.AppointmentTypeCodeUnique);
    }
}
