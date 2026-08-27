namespace IettFaultManagement.Api.Services;

/// <summary>
/// Belirli aralıklarla yeni DI scope açıp FaultAutomationProcessor'ı çalıştıran hosted service'tir.
/// Bir turdaki hata servisi durdurmaz; hata loglanır ve sonraki turda yeniden denenir.
/// </summary>
public sealed class FaultAutomationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<FaultAutomationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<FaultAutomationProcessor>()
                    .ProcessDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Ariza operasyon otomasyonu calistirilamadi.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
