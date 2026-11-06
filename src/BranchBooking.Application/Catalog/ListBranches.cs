using BranchBooking.Application.Abstractions;

namespace BranchBooking.Application.Catalog;

public sealed class ListBranches(IBranchRepository repository)
{
    public async Task<IReadOnlyList<BranchDto>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var branches = await repository.ListActiveAsync(cancellationToken);
        return branches.Select(b => b.ToDto()).ToList();
    }
}
