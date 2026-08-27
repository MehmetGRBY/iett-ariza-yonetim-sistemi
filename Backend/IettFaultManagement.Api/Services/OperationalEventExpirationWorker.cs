using IettFaultManagement.Api.Data;
using IettFaultManagement.Api.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Services;

/// <summary>
/// Bitiş zamanı dolan açık operasyon olaylarını kullanıcı müdahalesi gerektirmeden kapatır.
/// Böylece tabloda geçmiş zamanı bulunan bir kayıt "Açık" olarak kalmaz.
/// </summary>
public sealed class OperationalEventExpirationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OperationalEventExpirationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Uygulama açıldığında beklemeden mevcut süresi dolmuş kayıtları uzlaştırır.
        await CloseExpiredEventsAsync(stoppingToken);

        using var timer = new PeriodicTimer(CheckInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await CloseExpiredEventsAsync(stoppingToken);
    }

    private async Task CloseExpiredEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;
            var expiredEvents = await db.OperationalEvents
                .Where(item => item.Status == "OPEN" && item.EndsAt != null && item.EndsAt <= now)
                .ToListAsync(cancellationToken);

            foreach (var item in expiredEvents)
            {
                // Veritabanı kısıtı kapanmış operasyon olayı için RESOLVED kodunu kabul eder;
                // frontend bu teknik değeri kullanıcıya "Kapalı" olarak gösterir.
                item.Status = "RESOLVED";
                db.AuditLogs.Add(new AuditLog
                {
                    Action = "OPERATIONAL_EVENT_AUTO_CLOSED",
                    EntityType = "operational_events",
                    EntityId = item.Id,
                    OldValues = "{\"Status\":\"OPEN\"}",
                    NewValues = "{\"Status\":\"RESOLVED\"}",
                    Description = $"{item.EventNumber} numaralı operasyon olayı bitiş zamanı dolduğu için otomatik kapatıldı.",
                    CreatedAt = now
                });
            }

            if (expiredEvents.Count > 0)
                await db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Uygulama kapanırken çalışan sorgunun iptal edilmesi normaldir.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Süresi dolan operasyon olayları kapatılamadı.");
        }
    }
}
