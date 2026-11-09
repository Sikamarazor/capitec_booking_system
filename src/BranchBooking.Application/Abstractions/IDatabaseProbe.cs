namespace BranchBooking.Application.Abstractions;

/// <summary>Checks whether the primary database is reachable. Used by the readiness endpoint.</summary>
public interface IDatabaseProbe
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
