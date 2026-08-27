using IettFaultManagement.Api.Data;
using IettFaultManagement.Api.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Services;

/// <summary>
/// Müdahale kararına göre aynı garajdaki müsait çekici, hizmet aracı, yedek araç,
/// sürücü ve teknik ekibi adil son-atama sırasıyla seçer; çakışmayı önleyerek kaydeder.
/// </summary>
public sealed class FaultResourceAssignmentService(ApplicationDbContext db, AppNotificationService notifications)
{
    public async Task AssignRequiredAsync(Fault fault, Vehicle original, Driver? originalDriver,
        long? teamId, long userId, bool tow, bool service, bool replacement, bool driverCanContinue, DateTime now,
        long? towTruckId = null, long? serviceVehicleId = null, long? replacementVehicleId = null,
        long? replacementDriverId = null, bool canCompleteCurrentTrip = false)
    {
        if (tow) await AssignAsync(fault, "TOW_TRUCK", "Çekici", teamId, userId, now, true,
            preferredVehicleId: towTruckId);
        if (service) await AssignAsync(fault, "SERVICE_VEHICLE", "Hizmet", teamId, userId, now, true,
            preferredVehicleId: serviceVehicleId);
        if (replacement)
        {
            // Yedek araç seçilirken arızalı aracın devredilecek tüm görev saatleri de dikkate alınır.
            var vehicle = await AssignAsync(fault, "REPLACEMENT_VEHICLE", "PASSENGER", null, userId, now,
                !driverCanContinue, original.Id, replacementVehicleId, replacementDriverId);
            if (vehicle is not null)
            {
                var resource = await db.FaultResourceAssignments.Where(x => x.FaultId == fault.Id && x.ResourceType == "REPLACEMENT_VEHICLE" && x.VehicleId == vehicle.Id && x.IsActive).OrderByDescending(x => x.Id).FirstAsync();
                // Asıl arızada sürücü yoksa yedek araç mutlaka kaynak için
                // seçilen yedek sürücüyle devam eder.
                var taskDriver = driverCanContinue && originalDriver is not null
                    ? originalDriver
                    : resource.DriverId.HasValue ? await db.Drivers.FindAsync(resource.DriverId.Value) : null;
                if (taskDriver is not null) await TransferAsync(fault, original, taskDriver, vehicle,
                    driverCanContinue, canCompleteCurrentTrip, userId, now);
            }
        }
        await db.SaveChangesAsync();
    }

    private async Task<Vehicle?> AssignAsync(Fault fault, string resourceType, string token, long? teamId,
        long userId, DateTime now, bool requireDriver, long? taskSourceVehicleId = null,
        long? preferredVehicleId = null, long? preferredDriverId = null)
    {
        var query = db.Vehicles.Where(x => x.IsActive && x.GarageId == fault.GarageId && x.Id != fault.VehicleId && x.VehicleStatus.Code == "AVAILABLE" && !db.FaultResourceAssignments.Any(r => r.VehicleId == x.Id && r.IsActive));
        query = token == "PASSENGER"
            ? query.Where(x => EF.Functions.ILike(x.VehicleType.Name, "%Otobüs%") || EF.Functions.ILike(x.VehicleType.Name, "%Metrobüs%"))
            : query.Where(x => EF.Functions.ILike(x.VehicleType.Name, $"%{token}%"));

        // Kullanıcı formda belirli bir kaynak seçtiyse otomatik sıralama yerine
        // yalnızca o araç doğrulanır; başka garaj veya meşgul araç kabul edilmez.
        if (preferredVehicleId.HasValue) query = query.Where(x => x.Id == preferredVehicleId.Value);

        if (taskSourceVehicleId.HasValue)
        {
            // Aday aracın mevcut planı ile arızalı aracın devredilecek görevleri kesişiyorsa
            // bu araç seçilmez. Böylece aynı araca aynı saatte iki görev yazılması önlenir.
            query = query.Where(candidate => !db.TaskAssignments.Any(candidateAssignment =>
                candidateAssignment.VehicleId == candidate.Id && candidateAssignment.IsActive &&
                candidateAssignment.ServiceTask.IsActive &&
                db.TaskAssignments.Any(sourceAssignment =>
                    sourceAssignment.VehicleId == taskSourceVehicleId.Value && sourceAssignment.IsActive &&
                    sourceAssignment.ServiceTask.IsActive && sourceAssignment.ServiceTask.PlannedArrivalAt > now &&
                    sourceAssignment.ServiceTask.PlannedDepartureAt < candidateAssignment.ServiceTask.PlannedArrivalAt &&
                    sourceAssignment.ServiceTask.PlannedArrivalAt > candidateAssignment.ServiceTask.PlannedDepartureAt)));
        }

        var vehicle = await query.OrderBy(x => db.FaultResourceAssignments.Where(r => r.VehicleId == x.Id).Max(r => (DateTime?)r.AssignedAt) ?? DateTime.MinValue).ThenBy(x => x.DoorNumber).FirstOrDefaultAsync();
        if (vehicle is null)
        {
            if (preferredVehicleId.HasValue)
                throw new InvalidOperationException("Seçilen kaynak artık müsait değil veya aracın garajına ait değil.");
            await NotifyAsync(fault, resourceType, false, null, now); return null;
        }
        long? driverId = null;
        if (requireDriver)
        {
            var driverQuery = db.Drivers
                .Where(x => x.IsActive && x.GarageId == fault.GarageId &&
                    x.DriverType == "RESERVE" && x.AvailabilityStatus == "AVAILABLE" &&
                    !db.FaultResourceAssignments.Any(r => r.DriverId == x.Id && r.IsActive) &&
                    !db.TaskAssignments.Any(a => a.DriverId == x.Id && a.IsActive &&
                        a.ServiceTask.PlannedDepartureAt <= now && a.ServiceTask.PlannedArrivalAt >= now) &&
                    // Yedek sürücü arızalı aracın gelecekteki görevleriyle çakışan başka bir
                    // göreve sahipse seçilmez; kontrol yalnızca anlık müsaitliğe bırakılmaz.
                    (!taskSourceVehicleId.HasValue || !db.TaskAssignments.Any(candidateAssignment =>
                        candidateAssignment.DriverId == x.Id && candidateAssignment.IsActive &&
                        candidateAssignment.ServiceTask.IsActive &&
                        db.TaskAssignments.Any(sourceAssignment =>
                            sourceAssignment.VehicleId == taskSourceVehicleId.Value && sourceAssignment.IsActive &&
                            sourceAssignment.ServiceTask.IsActive && sourceAssignment.ServiceTask.PlannedArrivalAt > now &&
                        sourceAssignment.ServiceTask.PlannedDepartureAt < candidateAssignment.ServiceTask.PlannedArrivalAt &&
                        sourceAssignment.ServiceTask.PlannedArrivalAt > candidateAssignment.ServiceTask.PlannedDepartureAt))) &&
                    !db.PersonnelIncidents.Any(i => i.DriverId == x.Id && i.IsActive &&
                        i.Status != "CANCELLED" && (i.ReportStatus == "PENDING" || i.ExpectedReturnAt > now)));

            // Görevleri devralacak sürücüyü merkez seçtiyse başka bir sürücüye
            // sessizce geçilmez; seçilen kişinin hâlâ müsait olduğu doğrulanır.
            if (preferredDriverId.HasValue)
                driverQuery = driverQuery.Where(x => x.Id == preferredDriverId.Value);

            driverId = await driverQuery
                .OrderBy(x => db.FaultResourceAssignments.Where(r => r.DriverId == x.Id)
                    .Max(r => (DateTime?)r.AssignedAt) ?? DateTime.MinValue)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync();

            if (!driverId.HasValue)
            {
                if (preferredDriverId.HasValue)
                    throw new InvalidOperationException("Seçilen yedek sürücü artık müsait değil veya aracın garajına ait değil.");
                await NotifyAsync(fault, resourceType, false, null, now);
                return null;
            }
        }
        if (driverId.HasValue) { var driver = await db.Drivers.FindAsync(driverId.Value); if (driver is not null) driver.AvailabilityStatus = "ON_DUTY"; }
        var assignment = new FaultResourceAssignment { FaultId = fault.Id, ResourceType = resourceType, VehicleId = vehicle.Id, DriverId = driverId, TechnicianTeamId = teamId, Status = "ASSIGNED", AssignedAt = now, AssignedByUserId = userId, Description = resourceType switch { "TOW_TRUCK" => "Otomatik çekici atandı.", "SERVICE_VEHICLE" => "Teknik ekip için hizmet aracı atandı.", _ => "Kalan görevler için yedek araç atandı." }, IsActive = true };
        db.FaultResourceAssignments.Add(assignment);
        vehicle.VehicleStatusId = (await db.VehicleStatuses.SingleAsync(x => x.Code == "ON_DUTY")).Id;
        await db.SaveChangesAsync();
        db.FaultResourceStatusHistories.Add(new FaultResourceStatusHistory { ResourceAssignmentId = assignment.Id, NewStatus = "ASSIGNED", ChangedByUserId = userId, Description = assignment.Description, ChangedAt = now });
        await NotifyAsync(fault, resourceType, true, vehicle.DoorNumber, now);
        return vehicle;
    }

    private async Task NotifyAsync(Fault fault, string type, bool assigned, string? doorNumber, DateTime now)
    {
        var label = type switch { "TOW_TRUCK" => "çekici", "SERVICE_VEHICLE" => "hizmet aracı", _ => "yedek araç" };
        await notifications.NotifyOperationsAsync(fault.Id,fault.GarageId,
            assigned ? "Kaynak atandı" : "Kaynak bulunamadı",
            assigned ? $"{fault.FaultNumber} için {doorNumber} numaralı {label} atandı." : $"{fault.FaultNumber} için müsait {label} bulunamadı.",
            assigned ? "RESOURCE_ASSIGNED" : "RESOURCE_MISSING",now);
    }

    private async Task TransferAsync(Fault fault, Vehicle oldVehicle, Driver driver, Vehicle replacement,
        bool driverCanContinue, bool canCompleteCurrentTrip, long userId, DateTime now)
    {
        var assignments = await db.TaskAssignments.Include(x => x.ServiceTask).Where(x =>
            x.IsActive && x.VehicleId == oldVehicle.Id && x.ServiceTask.IsActive &&
            x.ServiceTask.PlannedArrivalAt > now &&
            (!canCompleteCurrentTrip || x.ServiceTaskId != fault.ServiceTaskId)).ToListAsync();
        if (assignments.Count == 0) return;
        var batch = new TaskTransferBatch { FaultId = fault.Id, OldVehicleId = oldVehicle.Id, NewVehicleId = replacement.Id, DriverId = driver.Id, GarageId = oldVehicle.GarageId, TransferType = "REPLACEMENT", TransferredTaskCount = assignments.Count, DriverCanContinue = driverCanContinue, IsAutomatic = true, TransferredByUserId = userId, TransferredAt = now, Description = "Arıza sonrası ileri görevler yedek araca aktarıldı." };
        db.TaskTransferBatches.Add(batch); await db.SaveChangesAsync();
        foreach (var old in assignments) { old.IsActive = false; old.EndedAt = now; db.TaskAssignments.Add(new TaskAssignment { ServiceTaskId = old.ServiceTaskId, VehicleId = replacement.Id, DriverId = driver.Id, TransferBatchId = batch.Id, AssignmentType = "REPLACEMENT", AssignedByUserId = userId, AssignedAt = now, IsActive = true, Description = batch.Description }); }
        var dutyIds = assignments.Select(x => x.ServiceTask.ServiceDutyId).Distinct();
        foreach (var duty in await db.ServiceDuties.Where(x => dutyIds.Contains(x.Id)).ToListAsync()) { duty.OriginalVehicleId = replacement.Id; duty.OriginalDriverId = driver.Id; }
        driver.AvailabilityStatus = "ON_DUTY";
    }
}
