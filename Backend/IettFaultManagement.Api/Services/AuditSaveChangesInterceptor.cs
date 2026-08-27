using System.Net;
using System.Security.Claims;
using System.Text.Json;
using IettFaultManagement.Api.Data;
using IettFaultManagement.Api.Models.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IettFaultManagement.Api.Services;

/// <summary>
/// EF Core SaveChanges öncesinde ekleme, güncelleme ve silme hareketlerini yakalar.
/// Hassas alanları hariç tutarak kullanıcı, rol, IP, eski ve yeni değerleri audit_logs tablosuna yazar.
/// </summary>
public sealed class AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor)
    : SaveChangesInterceptor
{
    private static readonly HashSet<string> AuditedEntities =
    [
        nameof(AppUser), nameof(Vehicle), nameof(Driver), nameof(TechnicianTeam),
        nameof(TeamMember), nameof(Fault), nameof(FaultAssignment),
        nameof(FaultResourceAssignment), nameof(RepairReport), nameof(ServiceTask),
        nameof(TaskAssignment), nameof(PersonnelIncident)
    ];

    private static readonly HashSet<string> SensitiveProperties =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "PasswordHash", "SecurityStamp", "Password", "AccessToken", "RefreshToken"
        };

    // C# sınıf adlarının işlem günlüğü açıklamasına İngilizce olarak yazılmasını önler.
    // Veritabanındaki entity/table kodu teknik amaçla korunur; personele gösterilen açıklama Türkçedir.
    private static readonly IReadOnlyDictionary<string, string> EntityDisplayNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(AppUser)] = "Kullanıcı",
            [nameof(Vehicle)] = "Araç",
            [nameof(Driver)] = "Sürücü",
            [nameof(TechnicianTeam)] = "Teknik ekip",
            [nameof(TeamMember)] = "Teknik ekip üyesi",
            [nameof(Fault)] = "Arıza",
            [nameof(FaultAssignment)] = "Arıza ekip ataması",
            [nameof(FaultResourceAssignment)] = "Arıza kaynak ataması",
            [nameof(RepairReport)] = "Teknik rapor",
            [nameof(ServiceTask)] = "Sefer görevi",
            [nameof(TaskAssignment)] = "Görev ataması",
            [nameof(PersonnelIncident)] = "Personel olayı"
        };

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AddAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditEntries(DbContext? context)
    {
        if (context is not ApplicationDbContext db) return;

        var changedEntries = db.ChangeTracker.Entries()
            .Where(x => x.Entity is not AuditLog &&
                        AuditedEntities.Contains(x.Metadata.ClrType.Name) &&
                        x.State is EntityState.Modified or EntityState.Deleted)
            .ToList();
        if (changedEntries.Count == 0) return;

        var httpContext = httpContextAccessor.HttpContext;
        var userId = long.TryParse(
            httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
            ? parsedUserId
            : (long?)null;
        var roleId = long.TryParse(
            httpContext?.User.FindFirstValue("roleId"), out var parsedRoleId)
            ? parsedRoleId
            : (long?)null;
        var ipAddress = httpContext?.Connection.RemoteIpAddress;

        foreach (var entry in changedEntries)
        {
            var entityClassName = entry.Metadata.ClrType.Name;
            var entityDisplayName = EntityDisplayNames.GetValueOrDefault(entityClassName, "Sistem kaydı");
            var oldValues = new Dictionary<string, object?>();
            var newValues = new Dictionary<string, object?>();

            foreach (var property in entry.Properties.Where(x => !SensitiveProperties.Contains(x.Metadata.Name)))
            {
                if (entry.State == EntityState.Deleted || property.IsModified)
                    oldValues[property.Metadata.Name] = property.OriginalValue;
                if (entry.State == EntityState.Modified && property.IsModified)
                    newValues[property.Metadata.Name] = property.CurrentValue;
            }

            if (oldValues.Count == 0 && newValues.Count == 0) continue;

            db.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                RoleId = roleId,
                Action = entry.State == EntityState.Deleted ? "DELETE" : "UPDATE",
                EntityType = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name,
                EntityId = ReadEntityId(entry),
                OldValues = JsonSerializer.Serialize(oldValues),
                NewValues = newValues.Count == 0 ? null : JsonSerializer.Serialize(newValues),
                Description = $"{entityDisplayName} kaydı güncellendi.",
                IpAddress = NormalizeAddress(ipAddress),
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    private static long? ReadEntityId(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey()?.Properties.SingleOrDefault();
        if (key is null) return null;
        var value = entry.Property(key.Name).CurrentValue;
        return value is null ? null : Convert.ToInt64(value);
    }

    private static IPAddress? NormalizeAddress(IPAddress? address) =>
        address?.IsIPv4MappedToIPv6 == true ? address.MapToIPv4() : address;
}
