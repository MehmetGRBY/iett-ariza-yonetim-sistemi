using IettFaultManagement.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Services;

/// <summary>
/// Sistem saatine göre görevlerin PLANNED, ACTIVE ve COMPLETED durumlarını senkronize eder;
/// böylece geçmiş seferler ekranda planlı olarak kalmaz.
/// </summary>
public sealed class TaskStatusSynchronizationService(
    IServiceScopeFactory scopeFactory, ILogger<TaskStatusSynchronizationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await SynchronizeAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Görev durumları zaman bilgisine göre güncellenemedi."); }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task SynchronizeAsync(CancellationToken ct)
    {
        using var scope=scopeFactory.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now=DateTime.UtcNow;

        await db.ServiceTasks.Where(x=>x.IsActive&&x.Status!="CANCELLED"&&x.PlannedArrivalAt<=now)
            .ExecuteUpdateAsync(setters=>setters
                .SetProperty(x=>x.Status,"COMPLETED")
                .SetProperty(x=>x.ActualDepartureAt,x=>x.ActualDepartureAt??x.PlannedDepartureAt)
                .SetProperty(x=>x.ActualArrivalAt,x=>x.ActualArrivalAt??x.PlannedArrivalAt)
                .SetProperty(x=>x.CompletedAt,x=>x.CompletedAt??x.PlannedArrivalAt),ct);

        await db.ServiceTasks.Where(x=>x.IsActive&&x.Status!="CANCELLED"&&
                x.PlannedDepartureAt<=now&&x.PlannedArrivalAt>now)
            .ExecuteUpdateAsync(setters=>setters
                .SetProperty(x=>x.Status,"IN_PROGRESS")
                .SetProperty(x=>x.ActualDepartureAt,x=>x.ActualDepartureAt??x.PlannedDepartureAt)
                .SetProperty(x=>x.ActualArrivalAt,(DateTime?)null)
                .SetProperty(x=>x.CompletedAt,(DateTime?)null),ct);

        await db.ServiceTasks.Where(x=>x.IsActive&&x.Status!="CANCELLED"&&x.PlannedDepartureAt>now)
            .ExecuteUpdateAsync(setters=>setters
                .SetProperty(x=>x.Status,"PLANNED")
                .SetProperty(x=>x.ActualDepartureAt,(DateTime?)null)
                .SetProperty(x=>x.ActualArrivalAt,(DateTime?)null)
                .SetProperty(x=>x.CompletedAt,(DateTime?)null),ct);

        var duties=await db.ServiceDuties.Include(x=>x.ServiceTasks)
            .Where(x=>x.IsActive&&x.Status!="CANCELLED").ToListAsync(ct);
        foreach(var duty in duties)
        {
            var tasks=duty.ServiceTasks.Where(x=>x.IsActive&&x.Status!="CANCELLED").ToList();
            if(tasks.Count==0)continue;
            if(tasks.All(x=>x.Status=="COMPLETED"))
            {
                duty.Status="COMPLETED";
                duty.CompletedAt=tasks.Max(x=>x.PlannedArrivalAt);
            }
            else if(tasks.Any(x=>x.Status=="IN_PROGRESS"))
            {
                duty.Status="ACTIVE"; duty.CompletedAt=null;
            }
            else
            {
                duty.Status="PLANNED"; duty.CompletedAt=null;
            }
        }
        await db.SaveChangesAsync(ct);

        // Bir atamanın IsActive değeri, o görevin güncel atamasını gösterir; görevin şu
        // anda devam ettiğini göstermez. Bu nedenle sürücü durumunu ayrıca saat aralığına
        // ve devam eden arıza kaynağı görevlerine göre yeniden hesaplarız.
        var driversOnScheduledTask = db.TaskAssignments
            .Where(assignment => assignment.IsActive && assignment.ServiceTask.IsActive &&
                assignment.ServiceTask.Status != "CANCELLED" &&
                assignment.ServiceTask.PlannedDepartureAt <= now &&
                assignment.ServiceTask.PlannedArrivalAt > now)
            .Select(assignment => assignment.DriverId);
        var driversOnFaultResource = db.FaultResourceAssignments
            .Where(resource => resource.IsActive && resource.DriverId.HasValue)
            .Select(resource => resource.DriverId!.Value);

        await db.Drivers
            .Where(driver => driver.IsActive && driver.AvailabilityStatus != "ON_LEAVE" &&
                driver.AvailabilityStatus != "PASSIVE" &&
                (driversOnScheduledTask.Contains(driver.Id) || driversOnFaultResource.Contains(driver.Id)) &&
                driver.AvailabilityStatus != "ON_DUTY")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(driver => driver.AvailabilityStatus, "ON_DUTY"), ct);

        await db.Drivers
            .Where(driver => driver.IsActive && driver.AvailabilityStatus == "ON_DUTY" &&
                !driversOnScheduledTask.Contains(driver.Id) && !driversOnFaultResource.Contains(driver.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(driver => driver.AvailabilityStatus, "AVAILABLE"), ct);

        // Araçlarda da yalnız gerçek zamanlı görev veya aktif arıza kaynağı bulunan kayıtlar
        // görevde kalır. Arızalı/tamirde/servis dışı durumlara kesinlikle müdahale edilmez.
        var vehiclesOnScheduledTask = db.TaskAssignments
            .Where(assignment => assignment.IsActive && assignment.ServiceTask.IsActive &&
                assignment.ServiceTask.Status != "CANCELLED" &&
                assignment.ServiceTask.PlannedDepartureAt <= now &&
                assignment.ServiceTask.PlannedArrivalAt > now)
            .Select(assignment => assignment.VehicleId);
        var vehiclesOnFaultResource = db.FaultResourceAssignments
            .Where(resource => resource.IsActive)
            .Select(resource => resource.VehicleId);
        var onDutyStatusId = await db.VehicleStatuses
            .Where(status => status.Code == "ON_DUTY")
            .Select(status => status.Id)
            .SingleAsync(ct);
        var availableStatusId = await db.VehicleStatuses
            .Where(status => status.Code == "AVAILABLE")
            .Select(status => status.Id)
            .SingleAsync(ct);

        await db.Vehicles
            .Where(vehicle => vehicle.IsActive && vehicle.VehicleStatusId == availableStatusId &&
                (vehiclesOnScheduledTask.Contains(vehicle.Id) || vehiclesOnFaultResource.Contains(vehicle.Id)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(vehicle => vehicle.VehicleStatusId, onDutyStatusId), ct);

        await db.Vehicles
            .Where(vehicle => vehicle.IsActive && vehicle.VehicleStatusId == onDutyStatusId &&
                !vehiclesOnScheduledTask.Contains(vehicle.Id) && !vehiclesOnFaultResource.Contains(vehicle.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(vehicle => vehicle.VehicleStatusId, availableStatusId), ct);
    }
}
