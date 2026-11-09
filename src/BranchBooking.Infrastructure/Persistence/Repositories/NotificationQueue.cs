using BranchBooking.Application.Abstractions;
using BranchBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BranchBooking.Infrastructure.Persistence.Repositories;

/// <summary>
/// Claims due notifications with one statement: the rows are chosen with <c>FOR UPDATE SKIP LOCKED</c> and leased
/// by setting <c>locked_until_utc</c>, so several Workers never receive the same message. A Worker that dies leaves
/// a lease that simply expires.
/// </summary>
public sealed class NotificationQueue(BranchBookingDbContext context, NpgsqlDataSource dataSource) : INotificationQueue
{
    private const string ClaimSql = """
        UPDATE notification_messages
        SET locked_until_utc = @locked_until
        WHERE id IN (
            SELECT id
            FROM notification_messages
            WHERE status = 'Pending'
              AND next_attempt_at_utc <= @now
              AND (locked_until_utc IS NULL OR locked_until_utc <= @now)
            ORDER BY next_attempt_at_utc, id
            LIMIT @batch
            FOR UPDATE SKIP LOCKED)
        RETURNING id
        """;

    public async Task<IReadOnlyList<NotificationMessage>> ClaimDueAsync(
        DateTimeOffset nowUtc, int batchSize, TimeSpan lease, CancellationToken cancellationToken)
    {
        var now = nowUtc.ToUniversalTime();

        var ids = new List<Guid>();
        await using (var command = dataSource.CreateCommand(ClaimSql))
        {
            command.Parameters.AddWithValue("now", now);
            command.Parameters.AddWithValue("locked_until", now + lease);
            command.Parameters.AddWithValue("batch", batchSize);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetGuid(0));
            }
        }

        if (ids.Count == 0)
        {
            return [];
        }

        // Loaded after the update, so the concurrency token (xmin) is the one the claim produced.
        return await context.NotificationMessages
            .Include(m => m.Booking).ThenInclude(b => b.Branch)
            .Include(m => m.Booking).ThenInclude(b => b.AppointmentType)
            .Where(m => ids.Contains(m.Id))
            .OrderBy(m => m.NextAttemptAtUtc)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);
    }
}
