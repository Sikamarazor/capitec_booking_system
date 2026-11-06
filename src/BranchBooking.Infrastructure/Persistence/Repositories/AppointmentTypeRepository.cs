using BranchBooking.Application.Abstractions;
using BranchBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BranchBooking.Infrastructure.Persistence.Repositories;

public sealed class AppointmentTypeRepository(BranchBookingDbContext context) : IAppointmentTypeRepository
{
    public async Task<IReadOnlyList<AppointmentType>> ListActiveAsync(CancellationToken cancellationToken) =>
        await context.AppointmentTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

    public Task<AppointmentType?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.AppointmentTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
}
