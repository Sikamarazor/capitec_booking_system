using BranchBooking.Application.Abstractions;
using BranchBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BranchBooking.Infrastructure.Persistence.Repositories;

public sealed class BranchRepository(BranchBookingDbContext context) : IBranchRepository
{
    public async Task<IReadOnlyList<Branch>> ListActiveAsync(CancellationToken cancellationToken) =>
        await context.Branches
            .AsNoTracking()
            .Include(b => b.OpeningHours)
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);

    public Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Branches
            .AsNoTracking()
            .Include(b => b.OpeningHours)
            .SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
}
