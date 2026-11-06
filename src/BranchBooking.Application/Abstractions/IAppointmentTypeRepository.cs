using BranchBooking.Domain.Entities;

namespace BranchBooking.Application.Abstractions;

public interface IAppointmentTypeRepository
{
    /// <summary>Active appointment types ordered by sort order, then name. Not tracked.</summary>
    Task<IReadOnlyList<AppointmentType>> ListActiveAsync(CancellationToken cancellationToken);

    /// <summary>The appointment type (active or not), or <c>null</c>. Not tracked.</summary>
    Task<AppointmentType?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
