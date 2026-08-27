using IettFaultManagement.Api.Data;
using IettFaultManagement.Api.Dtos;
using IettFaultManagement.Api.Extensions;
using IettFaultManagement.Api.Models.Database;
using IettFaultManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Controllers;

/// <summary>
/// Garajda görev dışındayken kontrol edilen araçlarda fark edilen arızaları,
/// aktif servis görevi akışını ve saha kaynaklarını etkilemeden kaydeder.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin,Merkez Yetkilisi")]
[Route("api/faults/non-task")]
public sealed class NonTaskFaultsController(
    ApplicationDbContext db,
    FaultResourceAssignmentService resourceService,
    FaultInterventionPolicy interventionPolicy) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateFaultRequest request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        // Görev dışı araç garaj kontrolünde sürücüsüz bulunabilir; test
        // sürüşü ve transferde ise aracı fiilen kullanan sürücü zorunludur.
        var validContexts = new[] { "TEST_DRIVE", "GARAGE_CHECK", "TRANSFER", "PRE_SERVICE_CHECK", "OTHER" };
        var context = request.OperationContext.Trim().ToUpperInvariant();
        if (!validContexts.Contains(context))
            return BadRequest(new { message = "Geçerli görev dışı arıza bağlamı seçin." });
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(x => x.IsActive &&
            x.DoorNumber.ToUpper() == request.DoorNumber.Trim().ToUpper(), cancellationToken);
        if (vehicle is null) return BadRequest(new { message = "Aktif araç bulunamadı." });

        // Yanlış modla kayıt açılmasını backend de engeller.
        var hasActiveTask = await db.TaskAssignments.AnyAsync(x => x.IsActive && x.VehicleId == vehicle.Id &&
            x.ServiceTask.IsActive && x.ServiceTask.PlannedDepartureAt <= now && x.ServiceTask.PlannedArrivalAt >= now,
            cancellationToken);
        if (hasActiveTask)
            return Conflict(new { message = "Araç şu anda aktif görevde. Aktif görev arızası olarak kaydedin." });
        var driverRequired = context is "TEST_DRIVE" or "TRANSFER";
        if (driverRequired && !request.DriverId.HasValue)
            return BadRequest(new { message = "Test sürüşü veya transfer için aracı kullanan sürücü zorunludur." });
        Driver? driver = null;
        if (request.DriverId.HasValue)
        {
            driver = await db.Drivers.SingleOrDefaultAsync(x => x.Id == request.DriverId && x.IsActive &&
                x.GarageId == vehicle.GarageId, cancellationToken);
            if (driver is null) return BadRequest(new { message = "Aynı garaja bağlı geçerli bir sürücü seçin." });
            var driverBusy = await db.TaskAssignments.AnyAsync(x => x.IsActive && x.DriverId == driver.Id &&
                x.ServiceTask.IsActive && x.ServiceTask.PlannedDepartureAt <= now && x.ServiceTask.PlannedArrivalAt >= now,
                cancellationToken);
            if (driverBusy) return Conflict(new { message = "Seçilen sürücü şu anda başka bir aktif görevde." });
        }

        var category = await db.FaultCategories.SingleOrDefaultAsync(x => x.Id == request.FaultCategoryId &&
            x.IsActive && x.ParentCategoryId != null, cancellationToken);
        if (category is null) return BadRequest(new { message = "Geçerli bir arıza alt kategorisi seçin." });
        if (request.MileageAtFailure < vehicle.CurrentMileage)
            return BadRequest(new { message = $"Kilometre {vehicle.CurrentMileage:N0} değerinden küçük olamaz." });
        if (await db.Faults.AnyAsync(x => x.VehicleId == vehicle.Id && x.IsActive && x.ClosedAt == null,
            cancellationToken))
            return Conflict(new { message = "Bu araç için zaten açık bir arıza bulunuyor. Yeni kayıt açmadan mevcut arızayı sonuçlandırın." });

        // Garajdaki araca çekici, hizmet aracı veya yedek araç gönderilmez; teknik ekip
        // arızayı doğrudan kendi garajında değerlendirir.
        // Görev dışı araç zaten garaj sürecindedir; belirsiz teknisyen kararı
        // üretmeden doğrudan garaj müdahalesi kabul edilir.
        var decision = interventionPolicy.DecideForNonTask(context, "MOVABLE", "NO");

        var assignedStatus = await db.FaultStatuses.SingleAsync(x => x.Code == "ASSIGNED_TO_TEAM", cancellationToken);
        var waitingTeamStatus = await db.FaultStatuses.SingleAsync(x => x.Code == "WAITING_TEAM", cancellationToken);
        var team = await db.TechnicianTeams.Where(x => x.GarageId == vehicle.GarageId && x.IsActive && x.IsAvailable &&
                !db.FaultAssignments.Any(a => a.TeamId == x.Id && a.IsActive && a.Fault.ClosedAt == null))
            // PostgreSQL artan sıralamada NULL değerleri sona bırakır. Hiç görev
            // almamış ekipleri açıkça öne alarak görevleri adil dağıtırız.
            .OrderBy(x => x.LastAssignedAt == null ? 0 : 1)
            .ThenBy(x => x.LastAssignedAt)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var dispatchSecondsJson = await db.SystemSettings.AsNoTracking()
            .Where(x => x.SettingKey == "presentation_dispatch_seconds" && x.IsActive)
            .Select(x => x.SettingValue).SingleOrDefaultAsync(cancellationToken);
        var dispatchSeconds = int.TryParse(dispatchSecondsJson, out var parsedDispatch)
            ? Math.Clamp(parsedDispatch, 1, 3600) : 10;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var fault = new Fault
        {
            FaultNumber = $"ARZ-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
            VehicleId = vehicle.Id, DriverId = driver?.Id, CreatedByUserId = User.UserId(), GarageId = vehicle.GarageId,
            FaultCategoryId = category.Id, FaultStatusId = team is null ? waitingTeamStatus.Id : assignedStatus.Id,
            ServiceTaskId = null, Description = request.Description.Trim(), MileageAtFailure = request.MileageAtFailure,
            Latitude = 0, Longitude = 0, LocationDescription = "Garaj kontrolünde tespit edildi.",
            OccurredAt = request.OccurredAt.Kind == DateTimeKind.Utc ? request.OccurredAt : request.OccurredAt.ToUniversalTime(),
            CreatedAt = now, IsActive = true, ResponseDueAt = now.AddMinutes(category.ResponseSlaMinutes),
            ResolutionDueAt = now.AddMinutes(category.ResolutionSlaMinutes)
        };
        db.Faults.Add(fault);
        await db.SaveChangesAsync(cancellationToken);

        if (team is not null)
        {
            db.FaultAssignments.Add(new FaultAssignment { FaultId = fault.Id, TeamId = team.Id,
                AssignedByUserId = User.UserId(), IsAutomatic = true, AssignedAt = now, IsActive = true });
            team.IsAvailable = false; team.LastAssignedAt = now; fault.FirstResponseAt = now;
        }

        db.FaultResponsePlans.Add(new FaultResponsePlan
        {
            FaultId = fault.Id, MobilityStatus = "MOVABLE",
            CanCompleteCurrentTrip = false, CanContinueRemainingTasks = false,
            OnSiteRepairPossible = decision.OnSiteRepairPossible, TowRequired = decision.TowRequired,
            ServiceVehicleRequired = decision.ServiceVehicleRequired,
            ReplacementVehicleRequired = false, DriverCanContinue = true,
            AssessmentNote = $"Görev dışı araç arızası {context} bağlamında kaydedildi.",
            AssessedByUserId = User.UserId(), AssessedAt = now, IsActive = true,
            OperationMode = "MANUAL", AutomationEnabled = team is not null,
            AutomationStatus = team is null ? "WAITING_TEAM" : "TEAM_ASSIGNED",
            NextAutomationAt = team is null ? null : now.AddSeconds(dispatchSeconds),
            PlannedRepairMinutes = decision.OnSiteRepairPossible == true ? category.OnsiteRepairMinutes : category.EstimatedRepairMinutes,
            PlannedRepairResult = category.AutoRepairResult
        });
        db.FaultStatusHistories.Add(new FaultStatusHistory
        {
            FaultId = fault.Id, NewStatusId = fault.FaultStatusId, ChangedByUserId = User.UserId(),
            ChangedByRoleId = await db.AppUsers.Where(x => x.Id == User.UserId()).Select(x => x.RoleId)
                .SingleAsync(cancellationToken), ChangedAt = now, IsSystemAction = true,
            Description = team is null ? $"{context} bağlamında arıza ekip bekleme sırasına alındı."
                : $"{context} bağlamında {team.Name} ekibine otomatik atandı."
        });
        vehicle.CurrentMileage = Math.Max(vehicle.CurrentMileage, request.MileageAtFailure);
        // Garajda veya test sürüşünde tespit edilen açık arıza da aracın
        // "Göreve Hazır" görünmesine izin vermemelidir.
        var faultyStatusId = await db.VehicleStatuses.Where(x => x.Code == "FAULTY")
            .Select(x => x.Id).SingleAsync(cancellationToken);
        if (vehicle.VehicleStatusId != faultyStatusId)
        {
            db.VehicleStatusHistories.Add(new VehicleStatusHistory
            {
                VehicleId = vehicle.Id, OldStatusId = vehicle.VehicleStatusId,
                NewStatusId = faultyStatusId, ChangedByUserId = User.UserId(),
                ChangedAt = now, FaultId = fault.Id,
                Description = "Görev dışı kontrolde tespit edilen aktif arıza nedeniyle araç arızalı duruma alındı."
            });
            vehicle.VehicleStatusId = faultyStatusId;
        }
        await db.SaveChangesAsync(cancellationToken);
        await resourceService.AssignRequiredAsync(fault, vehicle, driver, team?.Id, User.UserId(),
            decision.TowRequired, decision.ServiceVehicleRequired, false, true, now);
        await transaction.CommitAsync(cancellationToken);
        return CreatedAtAction("Details", "Faults", new { id = fault.Id }, new { fault.Id, fault.FaultNumber });
    }
}
