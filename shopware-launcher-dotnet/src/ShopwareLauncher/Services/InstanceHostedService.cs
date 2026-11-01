namespace ShopwareLauncher.Services;

/// <summary>Beim Start: Setup fuer laufende Instanzen nachholen. Danach jede Minute abgelaufene Instanzen loeschen.</summary>
public sealed class InstanceHostedService(InstanceService instances, ILogger<InstanceHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await instances.ResumeRunningAsync(stoppingToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Resume fehlgeschlagen");
        }

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await instances.RemoveExpiredAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Auto-Loeschen fehlgeschlagen");
            }
        }
    }
}
