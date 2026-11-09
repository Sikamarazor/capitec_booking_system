using BranchBooking.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BranchBooking.Infrastructure.Persistence;

public sealed class NpgsqlDatabaseProbe(NpgsqlDataSource dataSource, ILogger<NpgsqlDatabaseProbe> logger) : IDatabaseProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(Timeout);

        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(timeoutSource.Token);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(timeoutSource.Token);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // The caller cancelled; do not report that as "database down".
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database readiness check failed");
            return false;
        }
    }
}
