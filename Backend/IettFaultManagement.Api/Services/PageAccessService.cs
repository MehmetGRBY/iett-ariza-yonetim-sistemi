using System.Text.Json;
using IettFaultManagement.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Services;

/// <summary>
/// Rol bazlı sayfa görünürlüğünü system_settings tablosundaki tek JSON ayarından okur.
/// İşlem yapma yetkileri controller üzerindeki Authorize kurallarıyla ayrıca korunur.
/// </summary>
public sealed class PageAccessService(ApplicationDbContext db)
{
    public const string SettingKey = "role_page_access";

    public static readonly string[] AllPageKeys =
    [
        "dashboard", "faults", "tasks", "personnel-incidents", "vehicles", "garages",
        "drivers", "technicians", "inspections", "monitoring", "solutions",
        "operational-events", "users", "audit-logs", "system-settings"
    ];

    private static readonly IReadOnlyDictionary<string, string[]> Defaults =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Admin"] = AllPageKeys,
            ["Merkez Yetkilisi"] =
            [
                "dashboard", "faults", "tasks", "personnel-incidents", "vehicles", "garages",
                "inspections", "monitoring", "solutions", "operational-events"
            ],
            ["Garaj Yetkilisi"] =
            [
                "dashboard", "faults", "tasks", "personnel-incidents", "vehicles", "garages",
                "drivers", "technicians", "inspections", "monitoring", "solutions",
                "operational-events"
            ]
        };

    public async Task<IReadOnlyList<string>> GetAllowedPagesAsync(string role, CancellationToken ct = default)
    {
        // Adminin yönetim ekranını yanlışlıkla kendisine kapatması engellenir.
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase)) return AllPageKeys;

        var json = await db.SystemSettings.AsNoTracking()
            .Where(setting => setting.SettingKey == SettingKey && setting.IsActive)
            .Select(setting => setting.SettingValue)
            .SingleOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var matrix = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json);
                var roleEntry = matrix?.FirstOrDefault(entry =>
                    entry.Key.Equals(role, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(roleEntry?.Key))
                    return Normalize(roleEntry.Value.Value, includeDashboard: true);
            }
            catch (JsonException)
            {
                // Bozuk ayar uygulamayı kilitlemez; güvenli varsayılan rol listesine dönülür.
            }
        }

        return Defaults.TryGetValue(role, out var defaults)
            ? defaults
            : ["dashboard"];
    }

    public static Dictionary<string, string[]> NormalizeMatrix(string json)
    {
        var matrix = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json)
            ?? throw new JsonException("Rol-sayfa matrisi boş olamaz.");

        return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Admin"] = AllPageKeys,
            ["Merkez Yetkilisi"] = Normalize(
                matrix.GetValueOrDefault("Merkez Yetkilisi") ?? [], includeDashboard: true).ToArray(),
            ["Garaj Yetkilisi"] = Normalize(
                matrix.GetValueOrDefault("Garaj Yetkilisi") ?? [], includeDashboard: true).ToArray()
        };
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string> pages, bool includeDashboard)
    {
        var result = pages.Where(page => AllPageKeys.Contains(page, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (includeDashboard && !result.Contains("dashboard", StringComparer.OrdinalIgnoreCase))
            result.Insert(0, "dashboard");
        return result;
    }
}
