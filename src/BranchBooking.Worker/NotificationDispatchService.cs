using BranchBooking.Application.Notifications;
using Microsoft.Extensions.Options;

namespace BranchBooking.Worker;

/// <summary>Runs <see cref="DispatchNotifications"/> every few seconds until stopped. One bad pass never stops the loop.</summary>
public sealed class NotificationDispatchService(
    IServiceScopeFactory scopes,
    IOptions<NotificationOptions> options,
    ILogger<NotificationDispatchService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        logger.LogInformation(
            "Notification dispatcher started (channel {Channel}, every {Seconds} s, batch {Batch})",
            settings.Channel, settings.PollSeconds, settings.BatchSize);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.PollSeconds));
        try
        {
            do
            {
                await RunPassAsync(settings.BatchSize, stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task RunPassAsync(int batchSize, CancellationToken cancellationToken)
    {
        try
        {
            DispatchSummary summary;
            do
            {
                // A fresh scope per batch gives each batch its own DbContext and change tracker.
                await using var scope = scopes.CreateAsyncScope();
                summary = await scope.ServiceProvider.GetRequiredService<DispatchNotifications>().ExecuteAsync(cancellationToken);

                if (summary.Claimed > 0)
                {
                    logger.LogInformation(
                        "Dispatch pass: claimed {Claimed}, sent {Sent}, skipped {Skipped}, to retry {Retried}, failed {Failed}",
                        summary.Claimed, summary.Sent, summary.Skipped, summary.Retried, summary.Failed);
                }
            }
            while (summary.Claimed >= batchSize);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Notification dispatch pass failed; trying again on the next tick");
        }
    }
}
