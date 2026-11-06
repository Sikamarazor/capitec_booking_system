using BranchBooking.Domain.Entities;

namespace BranchBooking.Application.Abstractions;

public interface IBranchRepository
{
    /// <summary>Active branches ordered by name, with their opening hours. Not tracked.</summary>
    Task<IReadOnlyList<Branch>> ListActiveAsync(CancellationToken cancellationToken);

    /// <summary>The branch (active or not) with its opening hours, or <c>null</c>. Not tracked.</summary>
    Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
