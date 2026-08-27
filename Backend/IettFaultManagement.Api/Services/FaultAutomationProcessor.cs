using IettFaultManagement.Api.Data;
using IettFaultManagement.Api.Models.Database;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IettFaultManagement.Api.Services;

/// <summary>
/// Zamanı gelen arıza planlarını işler: seçilen kaynakları yola çıkarır/ulaştırır,
/// ekibi tamire başlatır ve başarılı kullanıcı kontrolünden sonra kapanışı tamamlar.
/// </summary>
public sealed class FaultAutomationProcessor(ApplicationDbContext context, AppNotificationService notifications)
{
    public async Task ProcessDueAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await ReconcileCompletedFaultsAsync(now, cancellationToken);
        var ids = await context.FaultResponsePlans.AsNoTracking()
            .Where(x => x.IsActive && x.AutomationEnabled &&
                x.AutomationStatus != "COMPLETED" && x.AutomationStatus != "FAILED" &&
                (!x.NextAutomationAt.HasValue || x.NextAutomationAt <= now))
            .OrderBy(x => x.NextAutomationAt).Select(x => x.Id).Take(20)
            .ToListAsync(cancellationToken);

        foreach (var id in ids)
        {
            try { await ProcessOneAsync(id, now, cancellationToken); }
            catch (Exception exception)
            {
                context.ChangeTracker.Clear();
                var plan = await context.FaultResponsePlans.FindAsync([id], cancellationToken);
                if (plan is not null)
                {
                    // EF Core'un genel üst mesajı yerine PostgreSQL/constraint gibi gerçek
                    // kök hata saklanır; yönetici sorunun nedenini doğrudan görebilir.
                    plan.LastAutomationError = exception.GetBaseException().Message;
                    plan.NextAutomationAt = now.AddMinutes(1);
                    await context.SaveChangesAsync(cancellationToken);
                }
            }
        }
    }

    private async Task ReconcileCompletedFaultsAsync(DateTime now, CancellationToken ct)
    {
        var faultIds = await context.Faults.AsNoTracking()
            // RESOLVED kayıtları burada uzlaştırılmaz. Başarılı kontrolün oluşturduğu
            // READY_TO_CLOSE planını yalnızca ProcessOneAsync kapatır; aksi hâlde
            // uzlaştırıcı kapanış zamanlayıcısıyla yarışıp planı erken COMPLETED yapar.
            .Where(fault => fault.FaultStatus.Code == "UNRESOLVED" ||
                            fault.FaultStatus.Code == "CLOSED" ||
                            fault.FaultStatus.Code == "CANCELLED")
            .Where(fault =>
                context.FaultResourceAssignments.Any(resource => resource.FaultId == fault.Id && resource.IsActive) ||
                context.FaultAssignments.Any(assignment => assignment.FaultId == fault.Id && assignment.IsActive) ||
                context.FaultResponsePlans.Any(plan => plan.FaultId == fault.Id && plan.AutomationStatus != "COMPLETED"))
            .Select(fault => fault.Id).ToListAsync(ct);

        if (faultIds.Count == 0) return;
        var systemUser = await context.AppUsers.OrderByDescending(user => user.PersonnelNumber == "ADM-0001")
            .FirstAsync(user => user.IsActive, ct);
        var availableStatus = await context.VehicleStatuses.SingleAsync(status => status.Code == "AVAILABLE", ct);
        var onDutyStatus = await context.VehicleStatuses.SingleAsync(status => status.Code == "ON_DUTY", ct);

        foreach (var faultId in faultIds)
        {
            var fault = await context.Faults.Include(item => item.FaultStatus).SingleAsync(item => item.Id == faultId, ct);
            var assignments = await context.FaultAssignments.Where(item => item.FaultId == faultId && item.IsActive).ToListAsync(ct);
            foreach (var assignment in assignments)
            {
                assignment.StartedAt ??= assignment.AssignedAt;
                assignment.IsActive = false;
                assignment.CompletedAt ??= now;
                var team = await context.TechnicianTeams.FindAsync([assignment.TeamId], ct);
                if (team is not null) team.IsAvailable = true;
                var members = await context.TeamMembers.Where(member => member.TeamId == assignment.TeamId && member.IsActive).ToListAsync(ct);
                foreach (var member in members) member.WorkStatus = "AVAILABLE";
            }

            var resources = await context.FaultResourceAssignments.Where(item => item.FaultId == faultId && item.IsActive).ToListAsync(ct);
            foreach (var resource in resources)
            {
                var oldStatus = resource.Status;
                resource.Status = fault.FaultStatus.Code == "CANCELLED" ? "CANCELLED" : "COMPLETED";
                resource.CompletedAt ??= now;
                resource.IsActive = false;
                AddResourceHistory(resource, oldStatus, resource.Status, systemUser.Id, now,
                    "Tamamlanmış arıza kaydıyla kaynak durumu otomatik uzlaştırıldı.");

                var hasFutureVehicleTask = await context.TaskAssignments.AnyAsync(item => item.VehicleId == resource.VehicleId &&
                    item.IsActive && item.ServiceTask.IsActive && item.ServiceTask.PlannedArrivalAt > now, ct);
                var resourceVehicle = await context.Vehicles.FindAsync([resource.VehicleId], ct);
                if (resourceVehicle is not null && resourceVehicle.IsActive)
                    resourceVehicle.VehicleStatusId = hasFutureVehicleTask ? onDutyStatus.Id : availableStatus.Id;

                if (resource.DriverId.HasValue)
                {
                    var hasFutureDriverTask = await context.TaskAssignments.AnyAsync(item => item.DriverId == resource.DriverId &&
                        item.IsActive && item.ServiceTask.IsActive && item.ServiceTask.PlannedArrivalAt > now, ct);
                    var resourceDriver = await context.Drivers.FindAsync([resource.DriverId.Value], ct);
                    if (resourceDriver is not null && resourceDriver.IsActive)
                        resourceDriver.AvailabilityStatus = hasFutureDriverTask ? "ON_DUTY" : "AVAILABLE";
                }
            }

            var plan = await context.FaultResponsePlans.SingleOrDefaultAsync(item => item.FaultId == faultId && item.IsActive, ct);
            if (plan is not null && plan.AutomationStatus != "COMPLETED")
            {
                plan.AutomationStatus = "COMPLETED";
                plan.AutomationEnabled = false;
                plan.AutomationCompletedAt ??= now;
                plan.NextAutomationAt = null;
                plan.LastAutomationError = null;
            }

            if (fault.FaultStatus.Code == "RESOLVED")
            {
                var vehicle = await context.Vehicles.FindAsync([fault.VehicleId], ct);
                if (vehicle is not null && vehicle.IsActive)
                {
                    var hasFutureTask = await context.TaskAssignments.AnyAsync(item => item.VehicleId == fault.VehicleId &&
                        item.IsActive && item.ServiceTask.IsActive && item.ServiceTask.PlannedArrivalAt > now, ct);
                    if (vehicle.VehicleStatusId != (hasFutureTask ? onDutyStatus.Id : availableStatus.Id))
                    {
                        context.VehicleStatusHistories.Add(new VehicleStatusHistory
                        {
                            VehicleId = vehicle.Id, OldStatusId = vehicle.VehicleStatusId,
                            NewStatusId = hasFutureTask ? onDutyStatus.Id : availableStatus.Id,
                            ChangedByUserId = systemUser.Id, ChangedAt = now, FaultId = fault.Id,
                            Description = "Çözülen arıza sonrası araç durumu otomatik uzlaştırıldı."
                        });
                        vehicle.VehicleStatusId = hasFutureTask ? onDutyStatus.Id : availableStatus.Id;
                    }
                }
            }
        }
        await context.SaveChangesAsync(ct);
        context.ChangeTracker.Clear();
    }

    private async Task ProcessOneAsync(long planId, DateTime now, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var plan = await context.FaultResponsePlans.SingleAsync(x => x.Id == planId, ct);
        var fault = await context.Faults.SingleAsync(x => x.Id == plan.FaultId, ct);
        var systemUser = await context.AppUsers.OrderByDescending(x => x.PersonnelNumber == "ADM-0001")
            .FirstAsync(x => x.IsActive, ct);

        var stepSeconds = await GetIntegerSettingAsync("presentation_dispatch_seconds", 10, ct);

        if (plan.AutomationStatus is "WAITING_CURRENT_TASK_END" or "WAITING_TODAYS_TASKS_END")
        {
            // Zamanlayıcı bu plana ancak mevcut görevin planlanan bitişinde ulaşır.
            // Araç hareket edebildiği için olay yerine kaynak göndermek yerine araç
            // kendi imkânıyla garaja dönüşe geçer.
            plan.AutomationStatus = "VEHICLE_RETURNING_TO_GARAGE";
            plan.NextAutomationAt = now.AddSeconds(stepSeconds);
            plan.LastAutomationError = null;
            var completedWork = plan.CanContinueRemainingTasks ? "Bugünkü görevler" : "Mevcut görev";
            await SetFaultStatusAsync(fault, "VEHICLE_RETURNING_TO_GARAGE", systemUser, now,
                $"{completedWork} tamamlandı; araç garaja doğru yola çıktı.", ct);
            await AddNotificationAsync(fault, "Araç garaja dönüyor",
                $"Araç {completedWork.ToLowerInvariant()} tamamladı ve tamir için garaja doğru yola çıktı.", now, ct);
        }
        else if (plan.AutomationStatus == "VEHICLE_RETURNING_TO_GARAGE")
        {
            await SetFaultStatusAsync(fault, "VEHICLE_DELIVERED", systemUser, now,
                "Araç görev sonrası garaja ulaştı.", ct);
            plan.AutomationStatus = "VEHICLE_DELIVERED";
            plan.NextAutomationAt = now.AddSeconds(stepSeconds);
        }
        else if (plan.AutomationStatus == "ON_SITE_REPAIRED_RETURNING")
        {
            // Yerinde tamir başarılı olsa da araç eski görevlerine dönmez. Garaja
            // ulaştığında kontrol kuyruğuna alınır; ancak başarılı kontrolden sonra
            // Göreve Hazır durumuna geçebilir.
            var resources = await context.FaultResourceAssignments
                .Where(x => x.FaultId == fault.Id && x.IsActive && x.ResourceType == "SERVICE_VEHICLE")
                .ToListAsync(ct);
            foreach (var resource in resources)
            {
                var old = resource.Status;
                resource.Status = "COMPLETED"; resource.CompletedAt = now; resource.IsActive = false;
                await ReleaseResourceAsync(resource, ct);
                AddResourceHistory(resource, old, "COMPLETED", systemUser.Id, now,
                    "Yerinde tamir tamamlandı; hizmet aracı ve ekip garaja döndü.");
            }
            await SetFaultStatusAsync(fault, "WAITING_INSPECTION", systemUser, now,
                "Yerinde tamir edilen araç garaja ulaştı; göreve hazır olmadan önce kontrol edilmelidir.", ct);
            plan.AutomationStatus = "WAITING_INSPECTION";
            plan.AutomationEnabled = false;
            plan.NextAutomationAt = null;
            await AddNotificationAsync(fault, "Araç kontrol bekliyor",
                "Yerinde tamir edilen araç garaja ulaştı. Başarılı kontrolden sonra yeni görev alabilir.", now, ct);
        }
        else if (plan.AutomationStatus is "PENDING" or "RESOURCE_DEPARTING")
        {
            await MarkPersonnelOnDutyAsync(fault.Id, ct);
            plan.AutomationStatus = "RESOURCE_EN_ROUTE";
            plan.NextAutomationAt = now.AddSeconds(stepSeconds);
            plan.LastAutomationError = null;
            await SetFaultStatusAsync(fault, "RESOURCES_EN_ROUTE", systemUser, now,
                "Kullanıcının seçtiği kaynaklar olay yerine doğru yola çıktı.", ct);
            await AddNotificationAsync(fault, "Kaynaklar yola çıktı",
                "Seçilen araçlar ve görevli personel olay yerine gidiyor.", now, ct);
            AddAutomationAudit(fault, systemUser, "FAULT_AUTOMATION_DISPATCHED", now,
                new { plan.AutomationStatus, plan.NextAutomationAt }, "Manuel seçilen kaynakların yola çıkışı simüle edildi.");
        }
        else if (plan.AutomationStatus is "DISPATCHED" or "RESOURCE_EN_ROUTE")
        {
            var resources = await context.FaultResourceAssignments
                .Where(x => x.FaultId == fault.Id && x.IsActive).ToListAsync(ct);
            foreach (var resource in resources)
            {
                var old = resource.Status;
                resource.DepartedAt ??= resource.AssignedAt;
                resource.ArrivedAt ??= now;
                resource.Status = "ARRIVED";
                AddResourceHistory(resource, old, resource.Status, systemUser.Id, now,
                    "Kaynak olay yerine ulaştı.");
            }
            await SetFaultStatusAsync(fault, "RESOURCES_ARRIVED", systemUser, now,
                "Kaynaklar olay yerine ulaştı.", ct);
            plan.AutomationStatus = "RESOURCE_ARRIVED";
            plan.NextAutomationAt = now.AddSeconds(stepSeconds);
        }
        else if (plan.AutomationStatus == "RESOURCE_ARRIVED")
        {
            if (plan.TowRequired)
            {
                var tow = await context.FaultResourceAssignments
                    .FirstOrDefaultAsync(x => x.FaultId == fault.Id && x.IsActive && x.ResourceType == "TOW_TRUCK", ct);
                if (tow is not null)
                {
                    var old = tow.Status;
                    tow.Status = "COMPLETED"; tow.CompletedAt = now; tow.IsActive = false;
                    await ReleaseResourceAsync(tow, ct);
                    AddResourceHistory(tow, old, "COMPLETED", systemUser.Id, now, "Arızalı araç garaja getirildi.");
                }
                await SetFaultStatusAsync(fault, "VEHICLE_DELIVERED", systemUser, now,
                    "Çekici arızalı aracı garaja getirdi.", ct);
                plan.AutomationStatus = "VEHICLE_DELIVERED";
            }
            else
            {
                await ContinueWithTeamOrQueueAsync(plan, fault, systemUser, now,
                    "Yerinde müdahale için teknik ekip arızaya atandı.", ct);
            }
            plan.NextAutomationAt = now.AddSeconds(stepSeconds);
        }
        else if (plan.AutomationStatus == "VEHICLE_DELIVERED")
        {
            await ContinueWithTeamOrQueueAsync(plan, fault, systemUser, now,
                "Garaja getirilen araç teknik ekibe atandı.", ct);
        }
        else if (plan.AutomationStatus == "TEAM_ASSIGNED")
        {
            var assignment = await context.FaultAssignments.SingleOrDefaultAsync(x => x.FaultId == fault.Id && x.IsActive, ct);
            if (assignment is not null) assignment.StartedAt ??= now;
            await SetFaultStatusAsync(fault, "REPAIR_IN_PROGRESS", systemUser, now,
                "Teknik ekip tamire başladı.", ct);
            await SetVehicleStatusAsync(fault.VehicleId, "UNDER_REPAIR", fault.Id, systemUser.Id, now,
                "Araç teknik ekip tarafından tamire alındı.", ct);
            // Kaynak hareketleri burada tamamlanır. Tamirin bitirilmesi, raporu ve
            // sonucu kullanıcıya ait olduğu için worker bu noktada durur.
            plan.AutomationStatus = "MANUAL_REPAIR_REQUIRED";
            plan.RepairStartedAt = now;
            plan.AutomationEnabled = false;
            plan.NextAutomationAt = null;
        }
        else if (plan.AutomationStatus == "REPAIRING")
        {
            // Önceki sürümden kalan REPAIRING planları da otomatik rapor üretmeden
            // güvenli biçimde manuel tamir aşamasında bekletilir.
            plan.AutomationStatus = "MANUAL_REPAIR_REQUIRED";
            plan.AutomationEnabled = false;
            plan.NextAutomationAt = null;
        }
        else if (plan.AutomationStatus == "READY_TO_CLOSE")
        {
            // Tamir edilen araç mevcut görevleri geri almaz. Görev, arıza sırasında
            // atanan yedek araçta kalır; tamir edilen araç yalnızca yeniden müsait
            // duruma geçirilir ve sonraki planlamalarda yeni bir görev alabilir.
            var activeResources = await context.FaultResourceAssignments
                .Where(x => x.FaultId == fault.Id && x.IsActive).ToListAsync(ct);
            foreach (var resource in activeResources)
            {
                var old = resource.Status;
                resource.Status = "COMPLETED";
                resource.CompletedAt = now;
                resource.IsActive = false;
                await ReleaseResourceAsync(resource, ct);
                AddResourceHistory(resource, old, "COMPLETED", systemUser.Id, now,
                    "Başarılı kontrol sonrası kaynak operasyonu tamamlandı.");
            }
            // Kontrol kaydı kullanıcı tarafından başarılı girildikten sonra
            // arıza yapılandırılmış görünür bekleme süresinin sonunda kapatılır.
            await SetFaultStatusAsync(fault, "CLOSED", systemUser, now,
                "Başarılı kontrolün ardından bekleme süresi tamamlandı; arıza otomatik kapatıldı.", ct);
            fault.ClosedAt = now;
            // Otomatik kapanış, manuel yaşam döngüsü servisi üzerinden geçmediği için
            // ana aracın tamirde kalmaması burada ayrıca güvenceye alınır.
            await SetVehicleStatusAsync(fault.VehicleId, "AVAILABLE", fault.Id, systemUser.Id, now,
                "Başarılı son kontrol ve otomatik kapanış sonrası araç göreve hazır duruma alındı.", ct);
            plan.ReadyToClose = false;
            plan.AutomationStatus = "COMPLETED";
            plan.AutomationEnabled = false;
            plan.AutomationCompletedAt = now;
            plan.NextAutomationAt = null;
            await AddNotificationAsync(fault, "Arıza otomatik kapatıldı",
                "Başarılı kontrol sonrası arıza kapatıldı.", now, ct);
            AddAutomationAudit(fault, systemUser, "FAULT_AUTOMATICALLY_CLOSED", now,
                new { plan.AutomationStatus, fault.ClosedAt }, "Başarılı kontrol sonrası otomatik kapanış tamamlandı.");
        }

        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>
    /// Kaynak operasyonu tamamlandığında aktif ekip ataması varsa tamir akışına geçer;
    /// yoksa arızayı FIFO ekip kuyruğunda bekletir. Kuyruk servisi ekip boşalınca devam ettirir.
    /// </summary>
    private async Task ContinueWithTeamOrQueueAsync(
        FaultResponsePlan plan, Fault fault, AppUser systemUser, DateTime now,
        string assignedDescription, CancellationToken ct)
    {
        var hasActiveAssignment = await context.FaultAssignments
            .AnyAsync(x => x.FaultId == fault.Id && x.IsActive, ct);

        // Kaynak hareketi sırasında önceden ekip seçilmemiş olabilir. Olay yerine
        // varıldığında aynı garajdaki müsait ekiplerden en uzun süredir iş almayanı
        // seçmek, boş ekip varken arızanın gereksiz biçimde kuyrukta kalmasını önler.
        if (!hasActiveAssignment)
        {
            var team = await context.TechnicianTeams
                .Where(x => x.GarageId == fault.GarageId && x.IsActive && x.IsAvailable &&
                    !context.FaultAssignments.Any(a => a.TeamId == x.Id && a.IsActive))
                .OrderBy(x => x.LastAssignedAt == null ? 0 : 1)
                .ThenBy(x => x.LastAssignedAt).ThenBy(x => x.Id)
                .FirstOrDefaultAsync(ct);
            if (team is not null)
            {
                context.FaultAssignments.Add(new FaultAssignment
                {
                    FaultId = fault.Id, TeamId = team.Id, AssignedByUserId = systemUser.Id,
                    IsAutomatic = true, AssignedAt = now, IsActive = true
                });
                team.IsAvailable = false;
                team.LastAssignedAt = now;
                fault.FirstResponseAt ??= now;
                var members = await context.TeamMembers
                    .Where(x => x.TeamId == team.Id && x.IsActive).ToListAsync(ct);
                foreach (var member in members) member.WorkStatus = "ON_DUTY";
                hasActiveAssignment = true;
            }
        }

        if (hasActiveAssignment)
        {
            plan.AutomationStatus = "TEAM_ASSIGNED";
            plan.NextAutomationAt = now.AddSeconds(await GetIntegerSettingAsync("presentation_dispatch_seconds", 10, ct));
            await SetFaultStatusAsync(fault, "ASSIGNED_TO_TEAM", systemUser, now, assignedDescription, ct);
            return;
        }

        plan.AutomationStatus = "WAITING_TEAM";
        plan.NextAutomationAt = null;
        await SetFaultStatusAsync(fault, "WAITING_TEAM", systemUser, now,
            "Garajdaki bütün teknik ekipler meşgul; arıza ekip bekleme sırasına alındı.", ct);
    }

    private async Task CompleteRepairAsync(FaultResponsePlan plan, Fault fault, AppUser systemUser, DateTime now, CancellationToken ct)
    {
        var repaired = !string.Equals(plan.PlannedRepairResult, "UNRESOLVED", StringComparison.OrdinalIgnoreCase);
        var assignment = await context.FaultAssignments.SingleOrDefaultAsync(x => x.FaultId == fault.Id && x.IsActive, ct);
        if (assignment is not null)
        {
            assignment.CompletedAt = now;
            assignment.IsActive = false;
            context.RepairReports.Add(new RepairReport
            {
                FaultAssignmentId = assignment.Id, CreatedByUserId = systemUser.Id,
                Result = repaired ? "REPAIRED" : "UNRESOLVED",
                Description = repaired
                    ? $"Yarı otomatik akışta {plan.PlannedRepairMinutes} dakikalık tahmine karşılık tamir başarılı tamamlandı."
                    : $"Yarı otomatik akışta {plan.PlannedRepairMinutes} dakikalık tahmine karşılık arıza çözülemedi.",
                StartedAt = plan.RepairStartedAt ?? now.AddMinutes(-plan.PlannedRepairMinutes),
                CompletedAt = now, SubmittedAt = now, IsSubmitted = true, IsActive = true, CreatedAt = now
            });
            var team = await context.TechnicianTeams.FindAsync([assignment.TeamId], ct);
            if (team is not null) team.IsAvailable = true;
            var members = await context.TeamMembers.Where(x => x.TeamId == assignment.TeamId && x.IsActive).ToListAsync(ct);
            foreach (var member in members) member.WorkStatus = "AVAILABLE";
        }

        var resources = await context.FaultResourceAssignments.Where(x => x.FaultId == fault.Id && x.IsActive).ToListAsync(ct);
        foreach (var resource in resources.Where(x => x.ResourceType != "REPLACEMENT_VEHICLE"))
        {
            var old = resource.Status;
            resource.Status = "COMPLETED"; resource.CompletedAt = now; resource.IsActive = false;
            await ReleaseResourceAsync(resource, ct);
            AddResourceHistory(resource, old, "COMPLETED", systemUser.Id, now, "Operasyon tamamlandi; kaynak garaja dondu.");
        }

        // Başarılı tamirde sistem burada durur. Kontrol kaydını ve sonrasındaki
        // kapatma kararını kullanıcı verir; worker otomatik kontrol oluşturmaz.
        var targetStatus = repaired ? "WAITING_INSPECTION" : "UNRESOLVED";
        await SetFaultStatusAsync(fault, targetStatus, systemUser, now,
            repaired ? "Tamir tamamlandı; kullanıcı tarafından kontrol kaydı girilmesi gerekiyor."
                     : "Teknik ekip arızanın çözülemediğini bildirdi.", ct);
        plan.AutomationStatus = repaired ? "WAITING_INSPECTION" : "COMPLETED";
        plan.AutomationEnabled = false;
        plan.AutomationCompletedAt = repaired ? null : now;
        plan.NextAutomationAt = null;
        plan.LastAutomationError = null;
        await AddNotificationAsync(fault, repaired ? "Kontrol kaydı gerekli" : "Arıza çözülemedi",
            repaired ? "Tamir tamamlandı. Arıza kapatılmadan önce kontrol kaydını siz girmelisiniz."
                     : "Tamir simülasyonu arızanın çözülemediği sonucuyla tamamlandı.", now, ct);
        AddAutomationAudit(fault, systemUser, repaired ? "FAULT_AUTOMATION_WAITING_INSPECTION" : "FAULT_AUTOMATION_UNRESOLVED", now,
            new { plan.AutomationStatus, plan.PlannedRepairMinutes },
            repaired ? "Tamir tamamlandı; otomasyon kontrol kaydı oluşturmadan durdu."
                     : "Tamir sonucu çözülemedi olarak kaydedildi.");
    }

    /// <summary>Sayısal sistem ayarını güvenli aralıkta okur.</summary>
    private async Task<int> GetIntegerSettingAsync(string key, int fallback, CancellationToken ct)
    {
        var json = await context.SystemSettings.AsNoTracking().Where(x => x.SettingKey == key && x.IsActive)
            .Select(x => x.SettingValue).SingleOrDefaultAsync(ct);
        return int.TryParse(json, out var value) ? Math.Clamp(value, 1, 3600) : fallback;
    }

    private async Task MarkPersonnelOnDutyAsync(long faultId, CancellationToken ct)
    {
        var resources = await context.FaultResourceAssignments.Where(x => x.FaultId == faultId && x.IsActive).ToListAsync(ct);
        foreach (var resource in resources)
        {
            if (resource.DriverId.HasValue)
            {
                var driver = await context.Drivers.FindAsync([resource.DriverId.Value], ct);
                if (driver is not null) driver.AvailabilityStatus = "ON_DUTY";
            }
            if (resource.TechnicianTeamId.HasValue)
            {
                var members = await context.TeamMembers.Where(x => x.TeamId == resource.TechnicianTeamId && x.IsActive).ToListAsync(ct);
                foreach (var member in members) member.WorkStatus = "ON_DUTY";
            }
        }
        var assignment = await context.FaultAssignments.SingleOrDefaultAsync(x => x.FaultId == faultId && x.IsActive, ct);
        if (assignment is not null)
        {
            var members = await context.TeamMembers.Where(x => x.TeamId == assignment.TeamId && x.IsActive).ToListAsync(ct);
            foreach (var member in members) member.WorkStatus = "ON_DUTY";
        }
    }

    private async Task ReleaseResourceAsync(FaultResourceAssignment resource, CancellationToken ct)
    {
        var available = await context.VehicleStatuses.SingleAsync(x => x.Code == "AVAILABLE", ct);
        var onDuty = await context.VehicleStatuses.SingleAsync(x => x.Code == "ON_DUTY", ct);
        var now = DateTime.UtcNow;
        var hasCurrentOrFutureVehicleTask = await context.TaskAssignments.AnyAsync(x =>
            x.VehicleId == resource.VehicleId && x.IsActive && x.ServiceTask.IsActive &&
            x.ServiceTask.PlannedArrivalAt > now, ct);
        var vehicle = await context.Vehicles.FindAsync([resource.VehicleId], ct);
        if (vehicle is not null) vehicle.VehicleStatusId = hasCurrentOrFutureVehicleTask ? onDuty.Id : available.Id;
        if (resource.DriverId.HasValue)
        {
            var hasCurrentOrFutureDriverTask = await context.TaskAssignments.AnyAsync(x =>
                x.DriverId == resource.DriverId && x.IsActive && x.ServiceTask.IsActive &&
                x.ServiceTask.PlannedArrivalAt > now, ct);
            var driver = await context.Drivers.FindAsync([resource.DriverId.Value], ct);
            if (driver is not null) driver.AvailabilityStatus = hasCurrentOrFutureDriverTask ? "ON_DUTY" : "AVAILABLE";
        }
    }

    private async Task SetFaultStatusAsync(Fault fault, string code, AppUser user, DateTime now, string description, CancellationToken ct)
    {
        var status = await context.FaultStatuses.SingleAsync(x => x.Code == code, ct);
        if (fault.FaultStatusId == status.Id) return;
        context.FaultStatusHistories.Add(new FaultStatusHistory
        { FaultId = fault.Id, OldStatusId = fault.FaultStatusId, NewStatusId = status.Id,
          ChangedByUserId = user.Id, ChangedByRoleId = user.RoleId, Description = description,
          IsSystemAction = true, ChangedAt = now });
        fault.FaultStatusId = status.Id;
    }

    private async Task SetVehicleStatusAsync(long vehicleId, string code, long faultId, long userId,
        DateTime now, string description, CancellationToken ct)
    {
        var vehicle = await context.Vehicles.FindAsync([vehicleId], ct);
        var status = await context.VehicleStatuses.SingleAsync(x => x.Code == code, ct);
        if (vehicle is null || vehicle.VehicleStatusId == status.Id) return;
        context.VehicleStatusHistories.Add(new VehicleStatusHistory
        { VehicleId = vehicle.Id, OldStatusId = vehicle.VehicleStatusId, NewStatusId = status.Id,
          ChangedByUserId = userId, ChangedAt = now, Description = description, FaultId = faultId });
        vehicle.VehicleStatusId = status.Id;
    }

    private void AddResourceHistory(FaultResourceAssignment resource, string oldStatus, string newStatus,
        long userId, DateTime now, string description) => context.FaultResourceStatusHistories.Add(new FaultResourceStatusHistory
        { ResourceAssignmentId = resource.Id, OldStatus = oldStatus, NewStatus = newStatus,
          ChangedByUserId = userId, Description = description, ChangedAt = now });

    private async Task AddNotificationAsync(Fault fault, string title, string message, DateTime now, CancellationToken ct)
        => await notifications.NotifyOperationsAsync(fault.Id, fault.GarageId, title,
            $"{fault.FaultNumber}: {message}", "FAULT_AUTOMATION", now, ct);

    private void AddAutomationAudit(Fault fault, AppUser user, string action, DateTime now,
        object newValues, string description) => context.AuditLogs.Add(new AuditLog
    {
        UserId = user.Id, RoleId = user.RoleId, Action = action,
        EntityType = "faults", EntityId = fault.Id,
        NewValues = JsonSerializer.Serialize(newValues), Description = description, CreatedAt = now
    });
}
