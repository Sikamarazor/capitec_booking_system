using BranchBooking.Application.Abstractions;

namespace BranchBooking.UnitTests.Support;

internal sealed class StubDatabaseProbe(bool isUp) : IDatabaseProbe
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) => Task.FromResult(isUp);
}
