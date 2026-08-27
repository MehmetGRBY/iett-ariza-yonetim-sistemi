using IettFaultManagement.Api.Data;
using IettFaultManagement.Api.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Services;

/// <summary>
/// Her gece bugünden sonraki belirlenen planlama ufkuna kadar eksik servis görevlerini oluşturur.
/// Arızalı/pasif araçları ve izinli sürücüleri elemeden geçirir; son kullanım zamanına göre adil atar.
/// </summary>
public sealed class RollingTaskPlanningService(
    IServiceScopeFactory scopeFactory, ILogger<RollingTaskPlanningService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureRollingWindowAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            var localNow=DateTime.Now;
            var nextRun=localNow.Date.AddDays(1).AddMinutes(5);
            await Task.Delay(nextRun-localNow,stoppingToken);
            await EnsureRollingWindowAsync(stoppingToken);
        }
    }

    private async Task EnsureRollingWindowAsync(CancellationToken ct)
    {
        try
        {
            var today=DateOnly.FromDateTime(DateTime.Today);
            for(var day=1;day<=3;day++)await EnsureDateAsync(today.AddDays(day),ct);
            await RepairFuturePlansAsync(today,ct);
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){ }
        catch(Exception ex){logger.LogError(ex,"Üç günlük kayan görev planı oluşturulamadı.");}
    }

    private async Task RepairFuturePlansAsync(DateOnly today,CancellationToken ct)
    {
        using var scope=scopeFactory.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now=DateTime.UtcNow;
        var duties=await db.ServiceDuties.Include(x=>x.ServiceTasks).ThenInclude(x=>x.TaskAssignments)
            .ThenInclude(x=>x.Vehicle).ThenInclude(x=>x.VehicleStatus)
            .Include(x=>x.ServiceTasks).ThenInclude(x=>x.TaskAssignments)
            .ThenInclude(x=>x.Driver).Where(x=>x.IsActive&&x.ServiceDate>=today).OrderBy(x=>x.ServiceDate).ThenBy(x=>x.Id).ToListAsync(ct);
        var vehicles=await db.Vehicles.Include(x=>x.VehicleStatus).Where(x=>x.IsActive).ToListAsync(ct);
        var drivers=await db.Drivers.Where(x=>x.IsActive).ToListAsync(ct);
        var vehicleLastAssignments=await db.TaskAssignments.GroupBy(x=>x.VehicleId)
            .Select(x=>new{x.Key,Last=x.Max(a=>a.AssignedAt)}).ToDictionaryAsync(x=>x.Key,x=>x.Last,ct);
        var driverLastAssignments=await db.TaskAssignments.GroupBy(x=>x.DriverId)
            .Select(x=>new{x.Key,Last=x.Max(a=>a.AssignedAt)}).ToDictionaryAsync(x=>x.Key,x=>x.Last,ct);
        var absences=await db.PersonnelIncidents.Where(x=>x.IsActive&&x.Status!="CANCELLED"&&
            (x.ReportStatus=="PENDING"||x.ExpectedReturnAt>now)).ToListAsync(ct);
        var reservations=duties.Select(d=>(
            Duty:d,
            Start:d.ServiceTasks.Min(t=>t.PlannedDepartureAt),
            End:d.ServiceTasks.Max(t=>t.PlannedArrivalAt),
            VehicleId:d.ServiceTasks.Where(t=>t.IsActive&&t.PlannedArrivalAt>now)
                .SelectMany(t=>t.TaskAssignments).Where(a=>a.IsActive).Select(a=>(long?)a.VehicleId).FirstOrDefault(),
            DriverId:d.ServiceTasks.Where(t=>t.IsActive&&t.PlannedArrivalAt>now)
                .SelectMany(t=>t.TaskAssignments).Where(a=>a.IsActive).Select(a=>(long?)a.DriverId).FirstOrDefault()
        )).ToList();
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        for(var index=0;index<reservations.Count;index++)
        {
            var item=reservations[index];
            if(!item.VehicleId.HasValue||!item.DriverId.HasValue)continue;
            var futureTasks=item.Duty.ServiceTasks.Where(t=>t.IsActive&&t.PlannedArrivalAt>now).ToList();
            var assignment=futureTasks.SelectMany(t=>t.TaskAssignments).FirstOrDefault(a=>a.IsActive);
            if(assignment is null)continue;
            var vehicleInvalid=!assignment.Vehicle.IsActive||assignment.Vehicle.VehicleStatus.Code is "FAULTY" or "WAITING_REPAIR" or "UNDER_REPAIR" or "OUT_OF_SERVICE";
            var driverInvalid=!assignment.Driver.IsActive||assignment.Driver.AvailabilityStatus=="PASSIVE"||
                absences.Any(a=>a.DriverId==assignment.DriverId&&a.AbsenceStartAt<item.End&&
                    (a.ReportStatus=="PENDING"||a.ExpectedReturnAt>item.Start));
            if(!vehicleInvalid&&!driverInvalid)continue;
            var vehicle=vehicleInvalid?vehicles.Where(v=>v.GarageId==item.Duty.GarageId&&v.VehicleTypeId==assignment.Vehicle.VehicleTypeId&&
                (v.VehicleStatus.Code is "AVAILABLE" or "IN_SERVICE" or "ON_DUTY")&&
                !reservations.Any(r=>r.Duty.Id!=item.Duty.Id&&r.VehicleId==v.Id&&r.Start<item.End&&r.End>item.Start))
                .OrderBy(v=>vehicleLastAssignments.GetValueOrDefault(v.Id,DateTime.MinValue)).ThenBy(v=>v.DoorNumber).FirstOrDefault():assignment.Vehicle;
            var driver=driverInvalid?drivers.Where(d=>d.GarageId==item.Duty.GarageId&&d.DriverType=="NORMAL"&&d.AvailabilityStatus!="PASSIVE"&&
                !absences.Any(a=>a.DriverId==d.Id&&a.AbsenceStartAt<item.End&&
                    (a.ReportStatus=="PENDING"||a.ExpectedReturnAt>item.Start))&&
                !reservations.Any(r=>r.Duty.Id!=item.Duty.Id&&r.DriverId==d.Id&&r.Start<item.End&&r.End>item.Start))
                .OrderBy(d=>driverLastAssignments.GetValueOrDefault(d.Id,DateTime.MinValue)).ThenBy(d=>d.PersonnelNumber).FirstOrDefault():assignment.Driver;
            if(vehicle is null||driver is null)continue;
            var oldAssignments=futureTasks.SelectMany(t=>t.TaskAssignments).Where(a=>a.IsActive).ToList();
            foreach(var old in oldAssignments){old.IsActive=false;old.EndedAt=now;}
            item.Duty.OriginalVehicleId=vehicle.Id;item.Duty.OriginalDriverId=driver.Id;
            await db.SaveChangesAsync(ct);
            foreach(var task in futureTasks)db.TaskAssignments.Add(new TaskAssignment
            {ServiceTaskId=task.Id,VehicleId=vehicle.Id,DriverId=driver.Id,AssignmentType="REPLACEMENT",AssignedByUserId=item.Duty.CreatedByUserId,
             AssignedAt=now,IsActive=true,Description="Uygun olmayan araç/şoför nedeniyle plan otomatik onarıldı."});
            await db.SaveChangesAsync(ct);
            reservations[index]=(item.Duty,item.Start,item.End,vehicle.Id,driver.Id);
            vehicleLastAssignments[vehicle.Id]=now;
            driverLastAssignments[driver.Id]=now;
        }
        await transaction.CommitAsync(ct);
    }

    private async Task EnsureDateAsync(DateOnly targetDate,CancellationToken ct)
    {
        using var scope=scopeFactory.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if(await db.ServiceDuties.AnyAsync(x=>x.IsActive&&x.ServiceDate==targetDate,ct))return;

        var templateDate=await db.ServiceDuties.Where(x=>x.IsActive&&x.ServiceDate<targetDate)
            .MaxAsync(x=>(DateOnly?)x.ServiceDate,ct);
        if(!templateDate.HasValue){logger.LogWarning("{TargetDate} için görev şablonu bulunamadı.",targetDate);return;}
        var template=await db.ServiceDuties.AsNoTracking().Include(x=>x.ServiceTasks)
            .Where(x=>x.IsActive&&x.ServiceDate==templateDate.Value)
            .OrderBy(x=>x.Id).ToListAsync(ct);
        if(template.Count==0)return;
        var systemUserId=await db.AppUsers.Where(x=>x.IsActive).OrderByDescending(x=>x.PersonnelNumber=="ADM-0001")
            .Select(x=>x.Id).FirstAsync(ct);
        var allVehicles=await db.Vehicles.AsNoTracking().Include(x=>x.VehicleStatus).ToListAsync(ct);
        var allDrivers=await db.Drivers.AsNoTracking().Where(x=>x.IsActive).ToListAsync(ct);
        var vehicleLastAssignments=await db.TaskAssignments.AsNoTracking().GroupBy(x=>x.VehicleId)
            .Select(x=>new{x.Key,Last=x.Max(a=>a.AssignedAt)}).ToDictionaryAsync(x=>x.Key,x=>x.Last,ct);
        var driverLastAssignments=await db.TaskAssignments.AsNoTracking().GroupBy(x=>x.DriverId)
            .Select(x=>new{x.Key,Last=x.Max(a=>a.AssignedAt)}).ToDictionaryAsync(x=>x.Key,x=>x.Last,ct);
        var dayStart=DateTime.SpecifyKind(targetDate.ToDateTime(TimeOnly.MinValue),DateTimeKind.Local).ToUniversalTime();
        var dayEnd=DateTime.SpecifyKind(targetDate.AddDays(2).ToDateTime(TimeOnly.MinValue),DateTimeKind.Local).ToUniversalTime();
        var absences=await db.PersonnelIncidents.AsNoTracking().Where(x=>x.IsActive&&x.Status!="CANCELLED"&&
            x.AbsenceStartAt<dayEnd&&(x.ReportStatus=="PENDING"||x.ExpectedReturnAt>dayStart)).ToListAsync(ct);
        var vehicleReservations=new List<(long Id,DateTime Start,DateTime End)>();
        var driverReservations=new List<(long Id,DateTime Start,DateTime End)>();
        var now=DateTime.UtcNow;
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        foreach(var sourceDuty in template)
        {
            var dutyNumber=ReplaceDateToken(sourceDuty.DutyNumber,templateDate.Value,targetDate);
            var sourceTasks=sourceDuty.ServiceTasks.Where(x=>x.IsActive).OrderBy(x=>x.SequenceNumber).ToList();
            var dutyStart=sourceTasks.Min(x=>MoveToDate(x.PlannedDepartureAt,templateDate.Value,targetDate));
            var dutyEnd=sourceTasks.Max(x=>MoveToDate(x.PlannedArrivalAt,templateDate.Value,targetDate));
            var sourceVehicle=sourceDuty.OriginalVehicleId.HasValue
                ? allVehicles.FirstOrDefault(x=>x.Id==sourceDuty.OriginalVehicleId.Value):null;
            var eligibleVehicles=allVehicles.Where(x=>x.IsActive&&x.GarageId==sourceDuty.GarageId&&
                (sourceVehicle is null||x.VehicleTypeId==sourceVehicle.VehicleTypeId)&&
                (x.VehicleStatus.Code is "AVAILABLE" or "IN_SERVICE" or "ON_DUTY")&&
                !vehicleReservations.Any(r=>r.Id==x.Id&&r.Start<dutyEnd&&r.End>dutyStart)).ToList();
            var vehicle=eligibleVehicles.OrderBy(x=>vehicleLastAssignments.GetValueOrDefault(x.Id,DateTime.MinValue))
                .ThenBy(x=>x.DoorNumber).FirstOrDefault();
            var eligibleDrivers=allDrivers.Where(x=>x.GarageId==sourceDuty.GarageId&&x.DriverType=="NORMAL"&&x.AvailabilityStatus!="PASSIVE"&&
                !absences.Any(a=>a.DriverId==x.Id&&a.AbsenceStartAt<dutyEnd&&
                    (a.ReportStatus=="PENDING"||a.ExpectedReturnAt>dutyStart))&&
                !driverReservations.Any(r=>r.Id==x.Id&&r.Start<dutyEnd&&r.End>dutyStart)).ToList();
            var driver=eligibleDrivers.OrderBy(x=>driverLastAssignments.GetValueOrDefault(x.Id,DateTime.MinValue))
                .ThenBy(x=>x.PersonnelNumber).FirstOrDefault();
            if(vehicle is not null)vehicleReservations.Add((vehicle.Id,dutyStart,dutyEnd));
            if(driver is not null)driverReservations.Add((driver.Id,dutyStart,dutyEnd));
            if(vehicle is not null)vehicleLastAssignments[vehicle.Id]=dutyStart;
            if(driver is not null)driverLastAssignments[driver.Id]=dutyStart;
            var duty=new ServiceDuty
            {
                DutyNumber=dutyNumber,ServiceDate=targetDate,GarageId=sourceDuty.GarageId,RouteId=sourceDuty.RouteId,
                OriginalVehicleId=vehicle?.Id,OriginalDriverId=driver?.Id,
                Status="PLANNED",Description="Üç günlük kayan planlama sistemi tarafından oluşturuldu.",
                CreatedByUserId=systemUserId,CreatedAt=now,IsActive=true
            };
            foreach(var sourceTask in sourceTasks)
            {
                var task=new ServiceTask
                {
                    TaskNumber=ReplaceDateToken(sourceTask.TaskNumber,templateDate.Value,targetDate),RouteId=sourceTask.RouteId,
                    ServiceDate=targetDate,SequenceNumber=sourceTask.SequenceNumber,
                    PlannedDepartureAt=MoveToDate(sourceTask.PlannedDepartureAt,templateDate.Value,targetDate),
                    PlannedArrivalAt=MoveToDate(sourceTask.PlannedArrivalAt,templateDate.Value,targetDate),
                    Status="PLANNED",IsActive=true,CreatedByUserId=systemUserId,CreatedAt=now
                };
                if(vehicle is not null&&driver is not null)
                    task.TaskAssignments.Add(new TaskAssignment
                    {
                        VehicleId=vehicle.Id,DriverId=driver.Id,
                        AssignmentType="ORIGINAL",AssignedByUserId=systemUserId,AssignedAt=now,IsActive=true,
                        Description="Günlük görev planı tarafından otomatik atandı."
                    });
                duty.ServiceTasks.Add(task);
            }
            db.ServiceDuties.Add(duty);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("{TargetDate} için {DutyCount} vardiya ve {TaskCount} görev oluşturuldu.",
            targetDate,template.Count,template.Sum(x=>x.ServiceTasks.Count(t=>t.IsActive)));
    }

    private static string ReplaceDateToken(string value,DateOnly source,DateOnly target)=>
        value.Replace(source.ToString("yyyyMMdd"),target.ToString("yyyyMMdd"),StringComparison.Ordinal);

    private static DateTime MoveToDate(DateTime value,DateOnly sourceDate,DateOnly targetDate)
    {
        var local=value.ToLocalTime();
        var dayOffset=DateOnly.FromDateTime(local).DayNumber-sourceDate.DayNumber;
        var targetLocal=targetDate.AddDays(dayOffset).ToDateTime(TimeOnly.FromDateTime(local));
        return DateTime.SpecifyKind(targetLocal,DateTimeKind.Local).ToUniversalTime();
    }
}
