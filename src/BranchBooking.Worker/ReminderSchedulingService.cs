using BranchBooking.Application.Notifications;
using Microsoft.Extensions.Options;

namespace BranchBooking.Worker;

/// <summary>Scans for bookings that need a reminder every <c>ScanIntervalSeconds</c> until stopped. One bad scan never stops the loop.</summary>
public sealed class ReminderSchedulingService(
    IServiceScopeFactory scopes,
    IOptions<ReminderOptions> options,
    ILogger<ReminderSchedulingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Reminder scheduling is disabled (Notifications:Reminders:Enabled is false)");
            return;
        }

        logger.LogInformation(
            "Reminder scheduling started (lead {Lead} h, minimum lead {Minimum} min, gap {Gap} h, every {Seconds} s)",
            settings.LeadHours, settings.MinimumLeadMinutes, settings.MinimumGapHours, settings.ScanIntervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.ScanIntervalSeconds));
        try
        {
            do
            {
                await RunScanAsync(settings.BatchSize, stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task RunScanAsync(int batchSize, CancellationToken cancellationToken)
    {
        try
        {
            ReminderSummary summary;
            do
            {
                await using var scope = scopes.CreateAsyncScope();
                summary = await scope.ServiceProvider.GetRequiredService<ScheduleReminders>().ExecuteAsync(cancellationToken);
            }
            while (summary.Found >= batchSize && summary.Queued > 0);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reminder scan failed; trying again on the next tick");
        }
    }
}
