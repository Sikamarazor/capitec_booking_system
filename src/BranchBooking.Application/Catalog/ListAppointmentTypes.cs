using BranchBooking.Application.Abstractions;

namespace BranchBooking.Application.Catalog;

public sealed class ListAppointmentTypes(IAppointmentTypeRepository repository)
{
    public async Task<IReadOnlyList<AppointmentTypeDto>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var types = await repository.ListActiveAsync(cancellationToken);
        return types.Select(t => t.ToDto()).ToList();
    }
}
