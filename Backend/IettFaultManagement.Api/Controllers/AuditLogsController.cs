using IettFaultManagement.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Controllers;

[ApiController, Authorize(Roles = "Admin"), Route("api/admin/audit-logs")]
/// <summary>
/// Admin'e sistemdeki veri değişikliklerini eski/yeni değerleriyle salt okunur ve
/// server-side sayfalı olarak sunar. Kayıt düzenleme veya silme endpointi içermez.
/// </summary>
public sealed class AuditLogsController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] string? recordType = null,
        [FromQuery] long? userId = null, [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null, [FromQuery] string? search = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.AuditLogs.AsNoTracking().AsQueryable();

        // Teknik tablo ve action kodlarını kullanıcıya ayrı ayrı filtreletmek yerine
        // aynı iş alanına ait bütün hareketleri tek kayıt türü altında toplar.
        query = recordType switch
        {
            "VEHICLE" => query.Where(x => x.EntityType == "vehicles" || x.EntityType == "Vehicle" ||
                x.EntityType == "vehicle_inspections" || x.Action == "BULK_DATA_ENRICHMENT" ||
                x.Action == "BULK_IDENTIFIER_NORMALIZATION" || x.Action == "ARV_HALF_DEACTIVATION_COMPLETED" ||
                x.Action == "SERVICE_TOW_MODEL_FINALIZATION" || x.Action == "VEHICLE_INSPECTION_CREATED"),
            "FAULT" => query.Where(x => x.EntityType == "faults" || x.EntityType == "Fault" ||
                x.EntityType == "fault_assignments" || x.EntityType == "fault_resource_assignments" ||
                x.EntityType == "repair_reports" || (x.Action.StartsWith("FAULT_") && !x.Action.StartsWith("FAULT_CATEGORY_")) ||
                x.Action == "REPAIR_REPORT_SUBMITTED"),
            "USER" => query.Where(x => x.EntityType == "app_users" || x.Action.StartsWith("USER_") ||
                x.Action == "PASSWORD_CHANGED" || x.Action == "DUPLICATE_GARAGE_MANAGER_DEACTIVATED"),
            "DRIVER" => query.Where(x => x.EntityType == "drivers" || x.Action.StartsWith("DRIVER_")),
            "GARAGE" => query.Where(x => x.EntityType == "garages" || x.EntityType == "Garage"),
            "TECHNICAL_TEAM" => query.Where(x => x.EntityType == "technician_teams" || x.EntityType == "team_members" ||
                x.Action.StartsWith("TECHNICIAN_")),
            "TASK" => query.Where(x => x.EntityType == "task_assignments" || x.EntityType == "service_tasks"),
            "PERSONNEL_INCIDENT" => query.Where(x => x.EntityType == "personnel_incidents" || x.Action.StartsWith("PERSONNEL_")),
            "OPERATIONAL_EVENT" => query.Where(x => x.EntityType == "operational_events" || x.Action.StartsWith("OPERATIONAL_EVENT_")),
            "FAULT_CATEGORY" => query.Where(x => x.EntityType == "fault_categories" || x.Action.StartsWith("FAULT_CATEGORY_")),
            "SOLUTION" => query.Where(x => x.EntityType == "solution_articles" || x.EntityType == "ai_suggestions" ||
                x.Action == "SOLUTION_LIBRARY_SEEDED" || x.Action == "AI_SUGGESTION_REVIEWED"),
            "SYSTEM" => query.Where(x => x.EntityType == "system_settings" || x.Action == "SYSTEM_SETTING_UPDATED" ||
                x.Action == "DATABASE_BACKEND_FINALIZATION" || (x.EntityType == "database_schema" &&
                x.Action != "BULK_DATA_ENRICHMENT" && x.Action != "BULK_IDENTIFIER_NORMALIZATION" &&
                x.Action != "ARV_HALF_DEACTIVATION_COMPLETED" && x.Action != "SERVICE_TOW_MODEL_FINALIZATION")),
            _ => query
        };
        if (userId.HasValue) query = query.Where(x => x.UserId == userId);
        if (startDate.HasValue)
        {
            var start = DateTime.SpecifyKind(startDate.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();
            query = query.Where(x => x.CreatedAt >= start);
        }
        if (endDate.HasValue)
        {
            var endExclusive = DateTime.SpecifyKind(endDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();
            query = query.Where(x => x.CreatedAt < endExclusive);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Action, pattern) ||
                EF.Functions.ILike(x.EntityType, pattern) ||
                (x.Description != null && EF.Functions.ILike(x.Description, pattern)) ||
                (x.User != null && (EF.Functions.ILike(x.User.PersonnelNumber, pattern) ||
                    EF.Functions.ILike(x.User.FirstName + " " + x.User.LastName, pattern))));
        }

        var totalCount = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Id, x.Action, x.EntityType, x.EntityId, x.Description,
                x.OldValues, x.NewValues,
                IpAddress = x.IpAddress == null ? null : x.IpAddress.ToString(),
                x.CreatedAt,
                User = x.User == null ? null : new
                {
                    x.User.Id, x.User.PersonnelNumber,
                    FullName = x.User.FirstName + " " + x.User.LastName
                },
                // Bazı eski ve interceptor kaynaklı kayıtlarda role_id boş bırakılmıştır.
                // Kullanıcı mevcutsa rolü kullanıcı kaydından tamamlayarak ekrandaki tutarsızlığı giderir.
                Role = x.Role != null ? x.Role.Name : x.User != null ? x.User.Role.Name : null
            }).ToListAsync();

        return Ok(new
        {
            items, page, pageSize, totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    // Kullanıcı filtresi veriden üretilir. Kayıt türleri ise frontend'de iş alanı
    // olarak tanımlıdır; ham action ve tablo adları filtre listesine gönderilmez.
    [HttpGet("filters")]
    public async Task<IActionResult> Filters() => Ok(new
    {
        users = await db.AuditLogs.AsNoTracking().Where(x => x.UserId != null)
            .Select(x => new { Id = x.UserId!.Value, x.User!.PersonnelNumber, FullName = x.User.FirstName + " " + x.User.LastName })
            .Distinct().OrderBy(x => x.PersonnelNumber).ToListAsync()
    });
}
