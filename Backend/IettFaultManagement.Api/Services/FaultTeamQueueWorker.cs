using System.Data;
using System.Text.Json;
using IettFaultManagement.Api.Data;
using IettFaultManagement.Api.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Services;

/// <summary>
/// Ekip bulunamadığı için bekleyen arızaları geliş sırasına göre tarar. Aynı garajda
/// ilk boşalan ekibi en eski bekleyen arızaya atar ve sunum otomasyonunu kaldığı yerden sürdürür.
/// </summary>
public sealed class FaultTeamQueueWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<FaultTeamQueueWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Uygulama açılır açılmaz mevcut kuyruk değerlendirilir; sonraki kontroller
        // kısa aralıklarla yapılarak ekip boşalmasına hızlı tepki verilir.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AssignWaitingFaultsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Ekip bekleme kuyruğu işlenirken hata oluştu.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task AssignWaitingFaultsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Serializable işlem ve veritabanındaki benzersiz aktif-atama indeksleri,
        // aynı ekibin eşzamanlı iki arızaya atanmasını birlikte engeller.
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var waitingStatusId = await db.FaultStatuses.AsNoTracking()
            .Where(x => x.Code == "WAITING_TEAM" && x.IsActive)
            .Select(x => x.Id)
            .SingleAsync(cancellationToken);
        var assignedStatusId = await db.FaultStatuses.AsNoTracking()
            .Where(x => x.Code == "ASSIGNED_TO_TEAM" && x.IsActive)
            .Select(x => x.Id)
            .SingleAsync(cancellationToken);
        var systemUser = await db.AppUsers.AsNoTracking()
            .OrderByDescending(x => x.PersonnelNumber == "ADM-0001")
            .FirstAsync(x => x.IsActive, cancellationToken);

        var waitingFaults = await db.Faults
            .Where(x => x.IsActive && x.ClosedAt == null && x.FaultStatusId == waitingStatusId &&
                !db.FaultAssignments.Any(a => a.FaultId == x.Id && a.IsActive))
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var fault in waitingFaults)
        {
            var team = await db.TechnicianTeams
                .Where(x => x.GarageId == fault.GarageId && x.IsActive && x.IsAvailable &&
                    !db.FaultAssignments.Any(a => a.TeamId == x.Id && a.IsActive))
                .OrderBy(x => x.LastAssignedAt == null ? 0 : 1)
                .ThenBy(x => x.LastAssignedAt)
                .ThenBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (team is null)
                continue;

            var now = DateTime.UtcNow;
            db.FaultAssignments.Add(new FaultAssignment
            {
                FaultId = fault.Id,
                TeamId = team.Id,
                AssignedByUserId = systemUser.Id,
                IsAutomatic = true,
                AssignedAt = now,
                IsActive = true
            });
            team.IsAvailable = false;
            team.LastAssignedAt = now;
            fault.FaultStatusId = assignedStatusId;
            fault.FirstResponseAt ??= now;

            db.FaultStatusHistories.Add(new FaultStatusHistory
            {
                FaultId = fault.Id,
                OldStatusId = waitingStatusId,
                NewStatusId = assignedStatusId,
                ChangedByUserId = systemUser.Id,
                ChangedByRoleId = systemUser.RoleId,
                Description = $"Bekleme sırasındaki arıza ilk boşalan {team.Name} ekibine otomatik atandı.",
                IsSystemAction = true,
                ChangedAt = now
            });

            var plan = await db.FaultResponsePlans
                .SingleOrDefaultAsync(x => x.FaultId == fault.Id && x.IsActive, cancellationToken);
            if (plan is not null)
            {
                // Normal modda da ekip kuyruğu yarı otomatik çalışır; ekip boşalınca
                // arıza tamire alınır, fakat tamir sonucu kullanıcı tarafından girilir.
                plan.AutomationEnabled = true;
                plan.AutomationStatus = "TEAM_ASSIGNED";
                var dispatchSecondsJson = await db.SystemSettings.AsNoTracking()
                    .Where(x => x.SettingKey == "presentation_dispatch_seconds" && x.IsActive)
                    .Select(x => x.SettingValue).SingleOrDefaultAsync(cancellationToken);
                var dispatchSeconds = int.TryParse(dispatchSecondsJson, out var parsedSeconds)
                    ? Math.Clamp(parsedSeconds, 1, 3600)
                    : 10;
                plan.NextAutomationAt = now.AddSeconds(dispatchSeconds);
                plan.LastAutomationError = null;
            }

            db.AuditLogs.Add(new AuditLog
            {
                UserId = systemUser.Id,
                RoleId = systemUser.RoleId,
                Action = "FAULT_TEAM_QUEUE_ASSIGNED",
                EntityType = "Fault",
                EntityId = fault.Id,
                NewValues = JsonSerializer.Serialize(new { fault.Id, TeamId = team.Id, team.Name }),
                Description = "Ekip bekleme kuyruğundaki arıza ilk müsait ekibe atandı.",
                CreatedAt = now
            });

            // Aynı döngüde ikinci arızanın aynı ekibi seçmemesi için her atama hemen kaydedilir.
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
