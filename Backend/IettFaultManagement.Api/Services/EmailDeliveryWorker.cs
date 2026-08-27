using System.Net;
using System.Net.Mail;
using IettFaultManagement.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Services;

/// <summary>
/// Outbox kuyruğundaki bekleyen e-postaları SMTP üzerinden gönderir. Beş başarısız
/// denemeden sonra kayıt FAILED olur; geçici hatalarda artan aralıkla tekrar denenir.
/// </summary>
public sealed class EmailDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<EmailDeliveryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (configuration.GetValue<bool>("Email:Enabled"))
                    await DeliverBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "E-posta outbox kuyruğu işlenirken hata oluştu.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task DeliverBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        // Uygulama bir e-posta gönderilirken kapanırsa kayıt PROCESSING
        // durumunda kalabilir. İki dakikadan eski yarım kalmış işlemleri
        // yeniden kuyruğa alarak bildirimin kalıcı olarak takılmasını önleriz.
        await db.EmailOutbox
            .Where(x => x.Status == "PROCESSING" && x.CreatedAt <= now.AddMinutes(-2))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, "RETRY")
                .SetProperty(x => x.NextRetryAt, now), cancellationToken);

        var ids = await db.EmailOutbox.AsNoTracking()
            .Where(x => (x.Status == "PENDING" || x.Status == "RETRY") &&
                (!x.NextRetryAt.HasValue || x.NextRetryAt <= now) && x.RetryCount < 5)
            .OrderBy(x => x.CreatedAt).Select(x => x.Id).Take(20)
            .ToListAsync(cancellationToken);

        foreach (var id in ids)
        {
            var item = await db.EmailOutbox.SingleAsync(x => x.Id == id, cancellationToken);
            try
            {
                item.Status = "PROCESSING";
                await db.SaveChangesAsync(cancellationToken);
                await SendAsync(item, cancellationToken);
                item.Status = "SENT";
                item.SentAt = DateTime.UtcNow;
                item.NextRetryAt = null;
                item.LastError = null;
            }
            catch (Exception exception)
            {
                item.RetryCount++;
                item.LastError = exception.GetBaseException().Message[..Math.Min(2000, exception.GetBaseException().Message.Length)];
                item.Status = item.RetryCount >= 5 ? "FAILED" : "RETRY";
                item.NextRetryAt = item.RetryCount >= 5
                    ? null
                    : DateTime.UtcNow.Add(RetryDelays[Math.Min(item.RetryCount - 1, RetryDelays.Length - 1)]);
                logger.LogWarning(exception, "{OutboxId} numaralı e-posta gönderilemedi.", item.Id);
            }
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SendAsync(Models.Database.EmailOutbox item, CancellationToken cancellationToken)
    {
        var host = configuration["Email:Smtp:Host"];
        var senderAddress = configuration["Email:SenderAddress"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(senderAddress))
            throw new InvalidOperationException("SMTP host ve gönderen adresi yapılandırılmalıdır.");

        using var message = new MailMessage
        {
            From = new MailAddress(senderAddress, configuration["Email:SenderName"] ?? "İETT Arıza Yönetim Sistemi"),
            Subject = item.Subject,
            Body = item.HtmlBody,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(item.RecipientEmail, item.RecipientName));

        using var client = new SmtpClient(host, configuration.GetValue("Email:Smtp:Port", 587))
        {
            EnableSsl = configuration.GetValue("Email:Smtp:EnableSsl", true),
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(
                configuration["Email:Smtp:Username"],
                configuration["Email:Smtp:Password"])
        };

        // SmtpClient CancellationToken kabul etmediğinden iptal isteği bağlantı
        // başlatılmadan önce denetlenir; gönderim yine asenkron gerçekleştirilir.
        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message);
    }
}
