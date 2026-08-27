using System.Text.Json;
using IettFaultManagement.Api.Data;
using IettFaultManagement.Api.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Services;

/// <summary>
/// Arızanın hangi durumdan hangi duruma geçebileceğini denetler. Kapanışta teknik rapor
/// zorunluluğunu uygular, geçmiş kaydı oluşturur ve ayrılan kaynakları tekrar müsait hale getirir.
/// </summary>
public sealed class FaultLifecycleService(ApplicationDbContext db, AppNotificationService notifications)
{
    private static readonly IReadOnlyDictionary<string, string[]> AllowedTransitions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["OPEN"] = ["SENT_TO_GARAGE", "CANCELLED"],
            ["SENT_TO_GARAGE"] = ["WAITING_TEAM", "ASSIGNED_TO_TEAM", "WAITING_REPAIR", "CANCELLED"],
            ["WAITING_TEAM"] = ["ASSIGNED_TO_TEAM", "CANCELLED"],
            ["ASSIGNED_TO_TEAM"] = ["WAITING_REPAIR", "REPAIR_IN_PROGRESS", "CANCELLED"],
            ["WAITING_REPAIR"] = ["REPAIR_IN_PROGRESS", "CANCELLED"],
            ["REPAIR_IN_PROGRESS"] = ["REPORT_SUBMITTED", "WAITING_INSPECTION", "UNRESOLVED"],
            ["REPORT_SUBMITTED"] = ["WAITING_INSPECTION", "UNRESOLVED"],
            ["WAITING_INSPECTION"] = ["INSPECTION_FAILED", "UNRESOLVED"],
            ["INSPECTION_FAILED"] = ["REPAIR_IN_PROGRESS", "UNRESOLVED", "CLOSED"],
            ["RESOLVED"] = ["CLOSED", "REOPENED"],
            ["UNRESOLVED"] = ["CLOSED", "REOPENED"],
            // Yeniden açılan arızanın önce ekip kuyruğuna girmesi gerekir. Aktif
            // atama oluşmadan doğrudan tamire geçmek raporun bağlanacağı kaydı yok eder.
            ["REOPENED"] = ["WAITING_TEAM", "CANCELLED"],
            ["CLOSED"] = ["REOPENED"],
            ["CANCELLED"] = ["REOPENED"]
        };

    /// <summary>
    /// Frontend'in yalnızca geçerli sonraki durumları gösterebilmesi için mevcut
    /// durum kodundan izin verilen hedef kodları döndürür.
    /// </summary>
    public IReadOnlyList<string> GetAllowedTargetCodes(string currentCode) =>
        AllowedTransitions.TryGetValue(currentCode, out var allowed) ? allowed : [];

    public async Task ApplyAsync(
        Fault fault,
        FaultStatus target,
        long userId,
        long roleId,
        string description,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var currentCode = fault.FaultStatus.Code;
        if (string.Equals(currentCode, target.Code, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Arıza zaten seçilen durumdadır.");

        if (!AllowedTransitions.TryGetValue(currentCode, out var allowed) ||
            !allowed.Contains(target.Code, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{currentCode} durumundan {target.Code} durumuna geçilemez.");

        // REPORT_SUBMITTED sıradan bir durum seçimi değildir. Bu durum yalnızca
        // teknik rapor endpoint'i gerçek RepairReport kaydını oluşturduğunda atanır.
        // Aksi halde arıza raporsuz biçimde kontrol aşamasına geçip kilitlenir.
        if (target.Code == "REPORT_SUBMITTED")
            throw new InvalidOperationException("Rapor Gönderildi durumu elle seçilemez. Teknik rapor formunu kullanın.");

        // Ekip gerektiren aşamalar aktif FaultAssignment olmadan seçilemez.
        // Özellikle yeniden açılan kayıtlarda eski atama kapanmış olduğundan
        // önce WAITING_TEAM üzerinden yeni bir ekip atanmalıdır.
        if (target.Code is "ASSIGNED_TO_TEAM" or "WAITING_REPAIR" or "REPAIR_IN_PROGRESS")
        {
            var hasActiveTeamAssignment = await db.FaultAssignments.AnyAsync(
                x => x.FaultId == fault.Id && x.IsActive,
                cancellationToken);

            if (!hasActiveTeamAssignment)
                throw new InvalidOperationException("Tamir aşamasına geçmeden önce arızaya aktif bir teknik ekip atanmalıdır.");
        }

        // Kontrol kuyruğuna yalnızca gerçekten gönderilmiş bir teknik raporu bulunan
        // arızalar alınabilir. Durum geçmişi tek başına rapor yerine geçmez.
        if (target.Code == "WAITING_INSPECTION")
        {
            var latestReportResult = await db.RepairReports
                .Where(x => x.FaultAssignment.FaultId == fault.Id && x.IsActive && x.IsSubmitted)
                .OrderByDescending(x => x.SubmittedAt)
                .Select(x => x.Result)
                .FirstOrDefaultAsync(cancellationToken);

            var hasSubmittedReport = latestReportResult is not null;

            if (!hasSubmittedReport)
                throw new InvalidOperationException("Araç kontrole gönderilmeden önce teknik rapor kaydedilmelidir.");

            if (latestReportResult == "UNRESOLVED")
                throw new InvalidOperationException("Çözülemedi sonucuna sahip teknik rapor araç kontrolüne gönderilemez.");
        }

        // Merkez yetkilisi arızayı çözüldü veya çözülemedi olarak sonuçlandırmadan önce
        // garajın teknik raporu bulunmalıdır. Böylece rapor aşaması atlanamaz.
        if (target.Code is "RESOLVED" or "UNRESOLVED")
        {
            var latestReport = await db.RepairReports
                .Where(x => x.FaultAssignment.FaultId == fault.Id && x.IsActive && x.IsSubmitted)
                .OrderByDescending(x => x.SubmittedAt)
                .Select(x => new { x.Result })
                .FirstOrDefaultAsync(cancellationToken);

            if (latestReport is null)
                throw new InvalidOperationException("Teknik rapor gönderilmeden arıza sonucu belirlenemez.");

            if (target.Code == "RESOLVED" && latestReport.Result == "UNRESOLVED")
                throw new InvalidOperationException("Çözülemedi sonucuna sahip teknik raporla arıza çözüldü olarak işaretlenemez.");

            if (target.Code == "UNRESOLVED" && latestReport.Result != "UNRESOLVED")
                throw new InvalidOperationException("Arıza çözülemedi yapılmadan önce teknik rapor sonucu Çözülemedi olmalıdır.");

            // Tamir edildi veya geçici olarak giderildi raporlarında merkez kararı,
            // garajın başarılı/koşullu araç kontrolünden sonra verilebilir.
            if (target.Code == "RESOLVED")
            {
                var inspectionPassed = await db.VehicleInspections.AnyAsync(
                    x => x.FaultId == fault.Id && (x.Result == "PASSED" || x.Result == "CONDITIONAL"),
                    cancellationToken);

                if (!inspectionPassed)
                    throw new InvalidOperationException("Arıza çözülmeden önce başarılı bir araç kontrolü kaydedilmelidir.");
            }
        }

        if (target.Code == "CLOSED")
        {
            var latestReport = await db.RepairReports
                .Where(x => x.FaultAssignment.FaultId == fault.Id && x.IsActive && x.IsSubmitted)
                .OrderByDescending(x => x.SubmittedAt)
                .Select(x => new { x.Result })
                .FirstOrDefaultAsync(cancellationToken);
            if (latestReport is null)
                throw new InvalidOperationException("Tamir raporu gönderilmeden arıza kapatılamaz.");

            // Tamir edildiği bildirilen araç, başarılı bir son kontrolden geçmeden
            // yeniden hizmete alınamaz. Çözülemeyen kayıtta kontrol aranmaz.
            var repaired = latestReport.Result is "REPAIRED" or "RESOLVED" or "TEMPORARY_REPAIR";
            if (repaired)
            {
                var inspectionPassed = await db.VehicleInspections.AnyAsync(
                    x => x.FaultId == fault.Id && (x.Result == "PASSED" || x.Result == "CONDITIONAL"),
                    cancellationToken);
                if (!inspectionPassed)
                    throw new InvalidOperationException("Arıza kapatılmadan önce başarılı bir araç kontrolü kaydedilmelidir.");
            }
        }

        var oldStatusId = fault.FaultStatusId;
        fault.FaultStatusId = target.Id;

        if (target.Code == "REOPENED")
        {
            fault.ClosedAt = null;
            fault.IsActive = true;
            fault.DeactivatedAt = null;
            fault.DeactivatedByUserId = null;
            fault.DeactivationReason = null;
            await SetMainVehicleOperationalStatusAsync(fault, "FAULTY", userId, now,
                "Arıza yeniden açıldığı için araç arızalı duruma alındı.", cancellationToken);
        }
        else if (target.IsClosedStatus)
        {
            fault.ClosedAt = now;
            fault.IsActive = target.Code != "CANCELLED";
            // Veritabanı constraint'i pasif kayıtta pasife alma tarihi ve gerekçesini
            // zorunlu tutar. İptal edilen hatalı arıza bu bilgilerle izlenebilir kalır.
            if (target.Code == "CANCELLED")
            {
                fault.DeactivatedAt = now;
                fault.DeactivatedByUserId = userId;
                fault.DeactivationReason = description.Trim();
            }
            await ReleaseResourcesAsync(fault.Id, userId, now, cancellationToken);

            // Ana aracın son durumu teknik rapor sonucuna göre belirlenir. Kaynak
            // araçların serbest bırakılmasından ayrı tutulur.
            if (target.Code == "CLOSED")
                await SetMainVehicleFinalStatusAsync(fault, userId, now, cancellationToken);
            else if (target.Code == "CANCELLED")
            {
                var hasCurrentOrFutureTask = await db.TaskAssignments.AnyAsync(x =>
                    x.VehicleId == fault.VehicleId && x.IsActive && x.ServiceTask.IsActive &&
                    x.ServiceTask.PlannedArrivalAt > now, cancellationToken);
                await SetMainVehicleOperationalStatusAsync(fault,
                    hasCurrentOrFutureTask ? "ON_DUTY" : "AVAILABLE", userId, now,
                    "Hatalı arıza kaydı iptal edildiği için araç operasyon durumuna geri alındı.", cancellationToken);
            }
        }

        db.FaultStatusHistories.Add(new FaultStatusHistory
        {
            FaultId = fault.Id,
            OldStatusId = oldStatusId,
            NewStatusId = target.Id,
            ChangedByUserId = userId,
            ChangedByRoleId = roleId,
            Description = description.Trim(),
            IsSystemAction = false,
            ChangedAt = now
        });

        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            RoleId = roleId,
            Action = "FAULT_STATUS_CHANGED",
            EntityType = "faults",
            EntityId = fault.Id,
            OldValues = JsonSerializer.Serialize(new { StatusId = oldStatusId, Code = currentCode }),
            NewValues = JsonSerializer.Serialize(new { StatusId = target.Id, target.Code }),
            Description = description.Trim(),
            CreatedAt = now
        });

        // Merkez sonucu belirlediğinde ilgili garaj yetkilisi arızanın sonucunu uygulama içinde görür.
        if (target.Code is "RESOLVED" or "UNRESOLVED")
        {
            var resultText = target.Code == "RESOLVED" ? "çözüldü" : "çözülemedi";
            await notifications.NotifyGarageAsync(fault.Id, fault.GarageId, "Merkez kararı verildi",
                $"{fault.FaultNumber} numaralı arıza merkez tarafından {resultText} olarak sonuçlandırıldı.",
                "FAULT_RESULT_DECIDED", now, cancellationToken);
        }
    }

    private async Task ReleaseResourcesAsync(long faultId, long userId, DateTime now, CancellationToken cancellationToken)
    {
        var resources = await db.FaultResourceAssignments
            .Where(x => x.FaultId == faultId && x.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var resource in resources)
        {
            resource.Status = "COMPLETED";
            resource.CompletedAt = now;
            resource.IsActive = false;

            var vehicle = await db.Vehicles.FindAsync([resource.VehicleId], cancellationToken);
            var hasCurrentOrFutureVehicleTask = await db.TaskAssignments.AnyAsync(x =>
                x.VehicleId == resource.VehicleId && x.IsActive && x.ServiceTask.IsActive &&
                x.ServiceTask.PlannedArrivalAt > now, cancellationToken);
            if (vehicle is not null)
            {
                var resourceStatusCode = hasCurrentOrFutureVehicleTask ? "ON_DUTY" : "AVAILABLE";
                vehicle.VehicleStatusId = await db.VehicleStatuses.Where(x => x.Code == resourceStatusCode)
                    .Select(x => x.Id).SingleAsync(cancellationToken);
            }

            if (resource.DriverId.HasValue)
            {
                var hasCurrentOrFutureDriverTask = await db.TaskAssignments.AnyAsync(x =>
                    x.DriverId == resource.DriverId && x.IsActive && x.ServiceTask.IsActive &&
                    x.ServiceTask.PlannedArrivalAt > now, cancellationToken);
                var driver = await db.Drivers.FindAsync([resource.DriverId.Value], cancellationToken);
                if (driver is not null && driver.IsActive)
                    driver.AvailabilityStatus = hasCurrentOrFutureDriverTask ? "ON_DUTY" : "AVAILABLE";
            }

            db.FaultResourceStatusHistories.Add(new FaultResourceStatusHistory
            {
                ResourceAssignmentId = resource.Id,
                OldStatus = "ASSIGNED",
                NewStatus = "COMPLETED",
                ChangedByUserId = userId,
                Description = "Arıza kapatıldığı için kaynak serbest bırakıldı.",
                ChangedAt = now
            });
        }

        var assignment = await db.FaultAssignments
            .Include(x => x.Team)
            .SingleOrDefaultAsync(x => x.FaultId == faultId && x.IsActive, cancellationToken);
        if (assignment is not null)
        {
            assignment.IsActive = false;
            // Tarih constraint'i tamamlanma zamanı bulunan atamada başlangıç
            // zamanını da ister; henüz başlamamış iptal kaydında atanma zamanı kullanılır.
            assignment.StartedAt ??= assignment.AssignedAt;
            assignment.CompletedAt = now;
            assignment.Team.IsAvailable = true;
            var members = await db.TeamMembers
                .Where(x => x.TeamId == assignment.TeamId && x.IsActive)
                .ToListAsync(cancellationToken);
            foreach (var member in members) member.WorkStatus = "AVAILABLE";
        }
    }

    private async Task SetMainVehicleFinalStatusAsync(
        Fault fault, long userId, DateTime now, CancellationToken cancellationToken)
    {
        var latestResult = await db.RepairReports
            .Where(x => x.FaultAssignment.FaultId == fault.Id && x.IsActive && x.IsSubmitted)
            .OrderByDescending(x => x.SubmittedAt)
            .Select(x => x.Result)
            .FirstAsync(cancellationToken);
        var targetCode = latestResult is "UNRESOLVED" or "FAILED" ? "OUT_OF_SERVICE" : "AVAILABLE";
        var targetStatusId = await db.VehicleStatuses.Where(x => x.Code == targetCode)
            .Select(x => x.Id).SingleAsync(cancellationToken);
        var vehicle = await db.Vehicles.FindAsync([fault.VehicleId], cancellationToken);
        if (vehicle is null || vehicle.VehicleStatusId == targetStatusId) return;

        db.VehicleStatusHistories.Add(new VehicleStatusHistory
        {
            VehicleId = vehicle.Id,
            OldStatusId = vehicle.VehicleStatusId,
            NewStatusId = targetStatusId,
            ChangedByUserId = userId,
            ChangedAt = now,
            FaultId = fault.Id,
            Description = targetCode == "AVAILABLE"
                ? "Başarılı kontrol ve merkez kapanışı sonrası araç hizmete alındı."
                : "Teknik raporda arıza çözülemediği için araç servis dışı bırakıldı."
        });
        vehicle.VehicleStatusId = targetStatusId;
    }

    private async Task SetMainVehicleOperationalStatusAsync(
        Fault fault, string targetCode, long userId, DateTime now, string description,
        CancellationToken cancellationToken)
    {
        var targetStatusId = await db.VehicleStatuses.Where(x => x.Code == targetCode)
            .Select(x => x.Id).SingleAsync(cancellationToken);
        var vehicle = await db.Vehicles.FindAsync([fault.VehicleId], cancellationToken);
        if (vehicle is null || vehicle.VehicleStatusId == targetStatusId) return;

        db.VehicleStatusHistories.Add(new VehicleStatusHistory
        {
            VehicleId = vehicle.Id, OldStatusId = vehicle.VehicleStatusId,
            NewStatusId = targetStatusId, ChangedByUserId = userId,
            ChangedAt = now, FaultId = fault.Id, Description = description
        });
        vehicle.VehicleStatusId = targetStatusId;
    }
}
