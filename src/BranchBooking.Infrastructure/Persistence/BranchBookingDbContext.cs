using BranchBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BranchBooking.Infrastructure.Persistence;

public sealed class BranchBookingDbContext(DbContextOptions<BranchBookingDbContext> options) : DbContext(options)
{
    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<AppointmentType> AppointmentTypes => Set<AppointmentType>();

    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BranchBookingDbContext).Assembly);
    }
}
