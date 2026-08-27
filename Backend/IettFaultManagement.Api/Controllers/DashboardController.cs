using IettFaultManagement.Api.Data;
using IettFaultManagement.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Merkez Yetkilisi,Garaj Yetkilisi")]
[Route("api/dashboard")]
/// <summary>Rol ve garaj kapsamına uygun filo, arıza, görev ve personel özetlerini dashboard için üretir.</summary>
public sealed class DashboardController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null, [FromQuery] long? garageId = null)
    {
        // Filtre gönderilmezse sistemin açılış tarihinden bugüne kadar olan bütün dönem
        // raporlanır. Böylece dashboard ilk açılışta ek bir filtreleme gerektirmez.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var selectedStart = startDate ?? new DateOnly(2026, 7, 20);
        var selectedEnd = endDate ?? today;
        if (selectedEnd < selectedStart)
            return BadRequest(new { message = "Bitiş tarihi başlangıç tarihinden önce olamaz." });

        var periodStart = DateTime.SpecifyKind(selectedStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();
        var periodEndExclusive = DateTime.SpecifyKind(selectedEnd.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();

        // Garaj yetkilisinin istemciden başka garaj göndermesi veri kapsamını değiştiremez.
        var scopedGarageId = User.IsInRole("Garaj Yetkilisi") ? User.GarageId() : garageId;

        var vehicles = db.Vehicles.AsNoTracking()
            .Where(x => !scopedGarageId.HasValue || x.GarageId == scopedGarageId);
        var faults = db.Faults.AsNoTracking()
            .Where(x => (!scopedGarageId.HasValue || x.GarageId == scopedGarageId) &&
                x.OccurredAt >= periodStart && x.OccurredAt < periodEndExclusive);
        var drivers = db.Drivers.AsNoTracking()
            .Where(x => !scopedGarageId.HasValue || x.GarageId == scopedGarageId);
        var activeTasks = db.ServiceTasks.AsNoTracking()
            .Where(x => x.IsActive && x.Status == "IN_PROGRESS" &&
                (!scopedGarageId.HasValue || x.TaskAssignments.Any(a =>
                    a.IsActive && a.Vehicle.GarageId == scopedGarageId)));

        // PostgreSQL tarih farkı hesaplarını sağlayıcıya bağımlı bırakmamak için yalnızca iki tarih
        // sütunu belleğe alınır ve ortalamalar güvenli biçimde uygulama katmanında hesaplanır.
        var repairPeriods = await db.RepairReports.AsNoTracking()
            .Where(x => x.IsActive && x.IsSubmitted && faults.Any(f => f.Id == x.FaultAssignment.FaultId))
            .Select(x => new { x.StartedAt, x.CompletedAt }).ToListAsync();
        var downtimePeriods = await faults.Where(x => x.ClosedAt != null)
            .Select(x => new { x.OccurredAt, x.ClosedAt }).ToListAsync();
        var averageRepairMinutes = repairPeriods.Count == 0 ? 0
            : Math.Round(repairPeriods.Average(x => Math.Max(0, (x.CompletedAt - x.StartedAt).TotalMinutes)), 1);
        var averageDowntimeMinutes = downtimePeriods.Count == 0 ? 0
            : Math.Round(downtimePeriods.Average(x => Math.Max(0, (x.ClosedAt!.Value - x.OccurredAt).TotalMinutes)), 1);

        return Ok(new
        {
            period = new { startDate = selectedStart, endDate = selectedEnd, garageId = scopedGarageId },
            totalVehicles = await vehicles.CountAsync(),
            activeVehicles = await vehicles.CountAsync(x => x.IsActive),
            totalDrivers = await drivers.CountAsync(x => x.IsActive),
            totalGarages = scopedGarageId.HasValue
                ? 1
                : await db.Garages.CountAsync(x => x.IsActive),
            activeTasks = await activeTasks.CountAsync(),
            completedTasksToday = await db.ServiceTasks.AsNoTracking().CountAsync(x =>
                x.IsActive && x.ServiceDate == today && x.Status == "COMPLETED" &&
                (!scopedGarageId.HasValue || x.TaskAssignments.Any(a =>
                    a.IsActive && a.Vehicle.GarageId == scopedGarageId))),
            availableVehicles = await vehicles.CountAsync(x => x.IsActive && x.VehicleStatus.Code == "AVAILABLE"),
            availableDrivers = await drivers.CountAsync(x => x.IsActive && x.AvailabilityStatus == "AVAILABLE"),
            availableTechnicianTeams = await db.TechnicianTeams.AsNoTracking().CountAsync(x =>
                x.IsActive && x.IsAvailable && (!scopedGarageId.HasValue || x.GarageId == scopedGarageId)),
            openFaults = await faults.CountAsync(x => x.IsActive && x.ClosedAt == null),
            closedFaults = await faults.CountAsync(x => x.ClosedAt != null),
            faultsOpenedToday = await db.Faults.AsNoTracking().CountAsync(x =>
                (!scopedGarageId.HasValue || x.GarageId == scopedGarageId) &&
                x.OccurredAt >= DateTime.SpecifyKind(today.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime() &&
                x.OccurredAt < DateTime.SpecifyKind(today.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime()),
            faultsClosedToday = await db.Faults.AsNoTracking().CountAsync(x => x.ClosedAt != null &&
                (!scopedGarageId.HasValue || x.GarageId == scopedGarageId) &&
                x.ClosedAt >= DateTime.SpecifyKind(today.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime() &&
                x.ClosedAt < DateTime.SpecifyKind(today.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime()),
            repairingVehicles = await vehicles.CountAsync(x => x.VehicleStatus.Code == "UNDER_REPAIR"),
            outOfServiceVehicles = await vehicles.CountAsync(x => x.VehicleStatus.Code == "OUT_OF_SERVICE"),
            // SLA göstergesi yerine operasyonun gerçek iş kuyrukları gösterilir.
            waitingInspectionFaults = await faults.CountAsync(x => x.IsActive && x.ClosedAt == null &&
                x.FaultStatus.Code == "WAITING_INSPECTION"),
            waitingTeamFaults = await faults.CountAsync(x => x.IsActive && x.ClosedAt == null &&
                x.FaultStatus.Code == "WAITING_TEAM"),
            criticalHealthVehicles = await db.VwVehicleHealthScores.AsNoTracking().CountAsync(x =>
                x.HealthScore < 50 && (!scopedGarageId.HasValue || x.GarageId == scopedGarageId)),
            completedFaults = await faults.CountAsync(x => x.ClosedAt != null),
            // Sonuçlandırılmamış personel olayları tarih filtresinden bağımsız güncel
            // operasyon yükünü gösterir; garaj yetkilisi yalnızca kendi garajını görür.
            openPersonnelIncidents = await db.PersonnelIncidents.AsNoTracking().CountAsync(x =>
                x.IsActive && x.Status != "RESOLVED" && x.Status != "CANCELLED" &&
                (!scopedGarageId.HasValue || x.GarageId == scopedGarageId)),
            resourceMissingFaults = await db.Notifications.AsNoTracking()
                .Where(x => x.NotificationType == "RESOURCE_MISSING" && x.FaultId != null &&
                    x.CreatedAt >= periodStart && x.CreatedAt < periodEndExclusive &&
                    faults.Any(f => f.Id == x.FaultId))
                .Select(x => x.FaultId).Distinct().CountAsync(),
            averageRepairMinutes,
            averageDowntimeMinutes,
            unreadNotifications = await db.Notifications.CountAsync(x =>
                x.UserId == User.UserId() && !x.IsRead),

            fleetByType = await vehicles.Where(x => x.IsActive)
                .GroupBy(x => x.VehicleType.Name)
                .Select(group => new { Name = group.Key, Count = group.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync(),

            // En çok arıza kaydı bulunan ilk 10 araç.
            topFaultyVehicles = await faults
                .GroupBy(x => new { x.Vehicle.Id, x.Vehicle.DoorNumber })
                .Select(group => new
                {
                    group.Key.Id,
                    group.Key.DoorNumber,
                    Count = group.Count()
                })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync(),

            // En fazla arıza bildiriminde adı geçen ilk 10 sürücü.
            topReportingDrivers = await faults
                // Garaj kontrolünde sürücüsüz açılan arızalar sürücü sıralamasına
                // boş bir personel satırı olarak girmemelidir.
                .Where(x => x.DriverId != null)
                .GroupBy(x => new
                {
                    x.Driver!.Id,
                    x.Driver.PersonnelNumber,
                    x.Driver.FirstName,
                    x.Driver.LastName
                })
                .Select(group => new
                {
                    group.Key.Id,
                    group.Key.PersonnelNumber,
                    FullName = group.Key.FirstName + " " + group.Key.LastName,
                    Count = group.Count()
                })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync(),

            // Seçilen dönemde adına en fazla aktif personel olayı kaydedilen ilk 10 sürücü.
            // Garaj filtresi hem Admin/Merkez seçimine hem de Garaj Yetkilisinin zorunlu kapsamına uyar.
            topPersonnelIncidentDrivers = await db.PersonnelIncidents.AsNoTracking()
                .Where(x => x.IsActive &&
                    x.OccurredAt >= periodStart && x.OccurredAt < periodEndExclusive &&
                    (!scopedGarageId.HasValue || x.GarageId == scopedGarageId))
                .GroupBy(x => new
                {
                    x.Driver.Id,
                    x.Driver.PersonnelNumber,
                    x.Driver.FirstName,
                    x.Driver.LastName
                })
                .Select(group => new
                {
                    group.Key.Id,
                    group.Key.PersonnelNumber,
                    FullName = group.Key.FirstName + " " + group.Key.LastName,
                    Count = group.Count()
                })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.FullName)
                .Take(10)
                .ToListAsync(),

            // Seçilen tarih ve garaj kapsamındaki en sık açılan ilk 10 alt arıza kategorisi.
            // Ana kategori de gönderilerek benzer isimli alt kategorilerin bağlamı korunur.
            topFaultCategories = await faults
                .GroupBy(x => new
                {
                    x.FaultCategory.Id,
                    x.FaultCategory.Name,
                    ParentName = x.FaultCategory.ParentCategory != null
                        ? x.FaultCategory.ParentCategory.Name
                        : null
                })
                .Select(group => new
                {
                    group.Key.Id,
                    group.Key.Name,
                    group.Key.ParentName,
                    Count = group.Count()
                })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Name)
                .Take(10)
                .ToListAsync(),

            faultsByGarage = await faults
                .GroupBy(x => x.Garage.Name)
                .Select(group => new { Garage = group.Key, Count = group.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync(),

            // Halka grafik sabit iki dilim yerine veritabanındaki gerçek durumları kullanır.
            faultsByStatus = await faults
                .GroupBy(x => new { x.FaultStatus.Code, x.FaultStatus.Name, x.FaultStatus.DisplayOrder })
                .Select(group => new { group.Key.Code, group.Key.Name, Count = group.Count(), group.Key.DisplayOrder })
                .OrderBy(x => x.DisplayOrder)
                .ToListAsync()
        });
    }
}
