namespace BranchBooking.Worker;

public sealed class HeartbeatService(ILogger<HeartbeatService> logger, TimeProvider clock) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker started at {TimestampUtc}", clock.GetUtcNow());

        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                logger.LogInformation("Worker heartbeat at {TimestampUtc}", clock.GetUtcNow());
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }
}
