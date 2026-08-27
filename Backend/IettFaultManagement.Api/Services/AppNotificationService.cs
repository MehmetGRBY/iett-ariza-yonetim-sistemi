using IettFaultManagement.Api.Data;
using IettFaultManagement.Api.Models.Database;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace IettFaultManagement.Api.Services;

/// <summary>
/// Arıza olaylarını ilgili kullanıcı rollerine uygulama içi bildirim olarak dağıtır.
/// Bildirimleri yalnızca DbContext'e ekler; işlemi başlatan controller/servis aynı transaction
/// içinde SaveChanges çağırarak iş kaydı ile bildirimin birlikte kaydedilmesini sağlar.
/// </summary>
public sealed class AppNotificationService(ApplicationDbContext db)
{
    public Task NotifyCentralAsync(long faultId, string title, string message, string type,
        DateTime now, CancellationToken cancellationToken = default) =>
        AddAsync(faultId, null, ["Admin", "Merkez Yetkilisi"], title, message, type, now, cancellationToken);

    public Task NotifyGarageAsync(long faultId, long garageId, string title, string message, string type,
        DateTime now, CancellationToken cancellationToken = default) =>
        AddAsync(faultId, garageId, ["Garaj Yetkilisi"], title, message, type, now, cancellationToken);

    public Task NotifyOperationsAsync(long faultId, long garageId, string title, string message, string type,
        DateTime now, CancellationToken cancellationToken = default) =>
        AddAsync(faultId, garageId, ["Admin", "Merkez Yetkilisi", "Garaj Yetkilisi"], title, message, type, now, cancellationToken);

    /// <summary>
    /// Kritik araç sağlığı uyarısını merkez yetkilileri ile yalnızca aracın bağlı
    /// bulunduğu garajın yetkilisine dağıtır.
    /// </summary>
    public Task NotifyVehicleHealthRiskAsync(long faultId, long garageId, string title, string message, string type,
        DateTime now, CancellationToken cancellationToken = default) =>
        AddAsync(faultId, garageId, ["Merkez Yetkilisi", "Garaj Yetkilisi"],
            title, message, type, now, cancellationToken);

    /// <summary>
    /// Operasyon olayını garaj kapsamına göre dağıtır. Garaj seçilmemişse bütün aktif
    /// garaj yetkilileri, seçilmişse yalnızca ilgili garajın yetkilisi bildirim alır.
    /// </summary>
    public Task NotifyOperationalEventAsync(long? garageId, string title, string message, string type,
        DateTime now, CancellationToken cancellationToken = default) =>
        AddAsync(null, garageId, ["Garaj Yetkilisi"], title, message, type, now, cancellationToken, true);

    private async Task AddAsync(long? faultId, long? garageId, string[] roles, string title,
        string message, string type, DateTime now, CancellationToken cancellationToken,
        bool notifyAllGaragesWhenUnspecified = false)
    {
        var recipients = await db.AppUsers
            .Where(x => x.IsActive && roles.Contains(x.Role.Name) &&
                (x.Role.Name != "Garaj Yetkilisi" || x.GarageId == garageId ||
                    (notifyAllGaragesWhenUnspecified && garageId == null)))
            .Select(x => new { x.Id, x.Email, FullName = x.FirstName + " " + x.LastName })
            .ToListAsync(cancellationToken);
        var recipientIds = recipients.Select(x => x.Id).ToList();

        // Aynı olay birkaç saniye içinde worker ve kullanıcı işlemi tarafından tekrar tetiklenirse
        // her kullanıcıya yalnızca bir bildirim bırakılır.
        var duplicateThreshold = now.AddMinutes(-1);
        var recentRecipientIds = await db.Notifications
            .Where(x => recipientIds.Contains(x.UserId) && x.FaultId == faultId &&
                x.NotificationType == type && x.Title == title && x.CreatedAt >= duplicateThreshold)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

        foreach (var recipient in recipients.Where(x => !recentRecipientIds.Contains(x.Id)))
        {
            db.Notifications.Add(new Notification
            {
                UserId = recipient.Id,
                FaultId = faultId,
                Title = title,
                Message = message,
                NotificationType = type,
                IsRead = false,
                CreatedAt = now
            });

            // E-posta adresi tanımlanmayan kullanıcı yalnızca uygulama içi bildirim
            // alır. Outbox kaydı aynı transaction'da oluşturulduğu için iş olayıyla
            // bildirim kanalları birbirinden kopmaz.
            if (!string.IsNullOrWhiteSpace(recipient.Email))
            {
                var encodedTitle = WebUtility.HtmlEncode(title);
                var encodedMessage = WebUtility.HtmlEncode(message).Replace("\n", "<br />");
                db.EmailOutbox.Add(new EmailOutbox
                {
                    UserId = recipient.Id,
                    FaultId = faultId,
                    NotificationType = type,
                    RecipientEmail = recipient.Email.Trim(),
                    RecipientName = recipient.FullName,
                    Subject = $"İETT AYS | {title}",
                    HtmlBody = faultId.HasValue
                        ? $"<h2>{encodedTitle}</h2><p>{encodedMessage}</p><p><strong>Arıza kayıt no:</strong> {faultId}</p>"
                        : $"<h2>{encodedTitle}</h2><p>{encodedMessage}</p>",
                    Status = "PENDING",
                    RetryCount = 0,
                    CreatedAt = now
                });
            }
        }
    }
}
