using IettFaultManagement.Api.Data;
using IettFaultManagement.Api.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Services;

/// <summary>
/// Personel olaylarında rapor bekleme ve rapor bitiş tarihlerini izler. Süre dolduğunda
/// sürücüyü tekrar müsait yapar, geçici kaynakları serbest bırakır ve olayı tamamlar.
/// </summary>
public sealed class PersonnelIncidentAutomationService(
    IServiceScopeFactory scopeFactory, ILogger<PersonnelIncidentAutomationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Personel olayı otomasyonu çalıştırılamadı."); }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        using var scope=scopeFactory.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now=DateTime.UtcNow;

        // ON_LEAVE değeri tek başına izin/rapor kanıtı değildir. Personelin gerçekten
        // izinli sayılabilmesi için aktif ve süresi devam eden bir olay kaydı bulunmalıdır.
        // Bu kontrol, eski test verilerinden veya yarıda kalan işlemlerden oluşan
        // "dayanaksız izinli" durumlarını otomatik olarak temizler.
        // Süresi bitmiş fakat henüz otomasyon tarafından kapatılmamış olaylar aşağıdaki
        // normal "izin bitti" akışında ele alınır; burada yalnızca hiç dayanağı olmayan
        // ON_LEAVE değerlerini hedefliyoruz.
        var recordedIncidentDriverIds = await db.PersonnelIncidents
            .Where(i => i.IsActive && i.Status != "CANCELLED")
            .Select(i => i.DriverId)
            .Distinct()
            .ToListAsync(ct);

        var staleLeaveDrivers = await db.Drivers
            .Where(driver => driver.IsActive && driver.AvailabilityStatus == "ON_LEAVE" &&
                !recordedIncidentDriverIds.Contains(driver.Id))
            .ToListAsync(ct);

        foreach (var driver in staleLeaveDrivers)
        {
            // Sürücünün şu anda devam eden bir görevi varsa durum ON_DUTY olmalıdır;
            // aksi durumda sürücü yeniden görev planlamasına uygun hale getirilir.
            var hasCurrentTask = await db.TaskAssignments.AnyAsync(assignment =>
                assignment.IsActive && assignment.DriverId == driver.Id &&
                assignment.ServiceTask.IsActive &&
                assignment.ServiceTask.PlannedDepartureAt <= now &&
                assignment.ServiceTask.PlannedArrivalAt >= now, ct);

            driver.AvailabilityStatus = hasCurrentTask ? "ON_DUTY" : "AVAILABLE";
            db.AuditLogs.Add(new AuditLog
            {
                UserId = null,
                Action = "DRIVER_STALE_LEAVE_RECONCILED",
                EntityType = "drivers",
                EntityId = driver.Id,
                Description = hasCurrentTask
                    ? "Aktif izin/rapor kaydı bulunmadığı ve devam eden görevi olduğu için şoför Görevde durumuna alındı."
                    : "Aktif izin/rapor kaydı bulunmadığı için şoför otomatik olarak Müsait durumuna alındı.",
                CreatedAt = now
            });
        }

        if (staleLeaveDrivers.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("{DriverCount} dayanaksız izin/rapor durumu düzeltildi.", staleLeaveDrivers.Count);
        }

        var finishedAbsenceDriverIds=db.PersonnelIncidents
            .Where(i=>i.IsActive&&i.Status!="CANCELLED"&&i.ReportStatus=="SUBMITTED"&&i.ExpectedReturnAt<=now).Select(i=>i.DriverId);
        var activeAbsenceDriverIds=db.PersonnelIncidents
            .Where(i=>i.IsActive&&i.Status!="CANCELLED"&&i.AbsenceStartAt<=now&&
                (i.ReportStatus=="PENDING"||i.ExpectedReturnAt>now)).Select(i=>i.DriverId);
        var returningDrivers=await db.Drivers
            .Where(x=>x.IsActive&&x.AvailabilityStatus=="ON_LEAVE"&&
                finishedAbsenceDriverIds.Contains(x.Id)&&!activeAbsenceDriverIds.Contains(x.Id))
            .ToListAsync(ct);
        foreach(var driver in returningDrivers)
        {
            driver.AvailabilityStatus="AVAILABLE";
            db.AuditLogs.Add(new AuditLog
            {
                UserId=null,Action="DRIVER_ABSENCE_ENDED",EntityType="drivers",EntityId=driver.Id,
                Description="Rapor/izin süresi sona erdi; şoför otomatik olarak müsait duruma alındı.",CreatedAt=now
            });
        }
        if(returningDrivers.Count>0)await db.SaveChangesAsync(ct);
        var waitingIds=await db.PersonnelIncidents.Where(x=>x.IsActive&&x.Status=="WAITING_REPLACEMENT")
            .OrderBy(x=>x.OccurredAt).Select(x=>x.Id).Take(20).ToListAsync(ct);
        foreach(var id in waitingIds) await TryDispatchAsync(db,id,now,ct);

        var arriving=await db.PersonnelIncidents.Where(x=>x.IsActive&&x.Status=="DISPATCHED"&&x.ArrivalDueAt<=now)
            .ToListAsync(ct);
        if(arriving.Count==0)return;
        var available=await db.VehicleStatuses.SingleAsync(x=>x.Code=="AVAILABLE",ct);
        foreach(var incident in arriving)
        {
            if(incident.ServiceVehicleId.HasValue)
            {
                var serviceVehicle=await db.Vehicles.FindAsync([incident.ServiceVehicleId.Value],ct);
                if(serviceVehicle is not null)serviceVehicle.VehicleStatusId=available.Id;
            }
            incident.Status="RESOLVED";incident.ResolvedAt=now;
            db.AuditLogs.Add(new AuditLog{UserId=incident.CreatedByUserId,Action="PERSONNEL_INCIDENT_RESOLVED",
                EntityType="personnel_incidents",EntityId=incident.Id,Description="Yedek şoför hizmet aracıyla ulaştı; görev devri tamamlandı.",CreatedAt=now});
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task TryDispatchAsync(ApplicationDbContext db,long id,DateTime now,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var incident=await db.PersonnelIncidents.SingleAsync(x=>x.Id==id,ct);
        var busy=db.TaskAssignments.Where(x=>x.IsActive&&x.ServiceTask.PlannedDepartureAt<=now&&x.ServiceTask.PlannedArrivalAt>=now).Select(x=>x.DriverId);
        var replacement=await db.Drivers.Where(x=>x.IsActive&&x.GarageId==incident.GarageId&&x.DriverType=="RESERVE"&&
            x.AvailabilityStatus=="AVAILABLE"&&!busy.Contains(x.Id)).OrderBy(x=>x.PersonnelNumber).FirstOrDefaultAsync(ct);
        var serviceVehicle=await db.Vehicles.Where(x=>x.IsActive&&x.GarageId==incident.GarageId&&x.VehicleStatus.Code=="AVAILABLE"&&
            EF.Functions.ILike(x.VehicleType.Name,"%Hizmet%")&&!db.PersonnelIncidents.Any(i=>i.ServiceVehicleId==x.Id&&i.IsActive&&i.Status=="DISPATCHED"))
            .OrderBy(x=>db.PersonnelIncidents.Where(i=>i.ServiceVehicleId==x.Id)
                .Max(i=>(DateTime?)i.CreatedAt)??DateTime.MinValue)
            .ThenBy(x=>x.DoorNumber).FirstOrDefaultAsync(ct);
        if(replacement is null||serviceVehicle is null||!incident.VehicleId.HasValue)return;

        var tasks=await db.TaskAssignments.Include(x=>x.ServiceTask).Where(x=>x.IsActive&&x.DriverId==incident.DriverId&&
            x.ServiceTask.PlannedArrivalAt>now&&
            (incident.ReportStatus=="PENDING"||!incident.ExpectedReturnAt.HasValue||x.ServiceTask.PlannedDepartureAt<incident.ExpectedReturnAt)).ToListAsync(ct);
        foreach(var old in tasks){old.IsActive=false;old.EndedAt=now;}
        await db.SaveChangesAsync(ct);
        foreach(var old in tasks)db.TaskAssignments.Add(new TaskAssignment
        {ServiceTaskId=old.ServiceTaskId,VehicleId=old.VehicleId,DriverId=replacement.Id,AssignmentType="REPLACEMENT",
         AssignedByUserId=incident.CreatedByUserId,AssignedAt=now,IsActive=true,Description="Personel olayı için otomatik yedek şoför devri."});
        replacement.AvailabilityStatus="ON_DUTY";
        var onDuty=await db.VehicleStatuses.SingleAsync(x=>x.Code=="ON_DUTY",ct);serviceVehicle.VehicleStatusId=onDuty.Id;
        incident.ReplacementDriverId=replacement.Id;incident.ServiceVehicleId=serviceVehicle.Id;
        incident.TransferredTaskCount=tasks.Count;incident.Status="DISPATCHED";incident.DispatchedAt=now;incident.ArrivalDueAt=now.AddMinutes(5);
        db.AuditLogs.Add(new AuditLog{UserId=incident.CreatedByUserId,Action="PERSONNEL_INCIDENT_DISPATCHED",
            EntityType="personnel_incidents",EntityId=incident.Id,Description="Bekleyen olay için kaynak bulundu ve görevler devredildi.",CreatedAt=now});
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
