using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IettFaultManagement.Api.Models.Database;

/// <summary>
/// Uygulama bildirimiyle birlikte üretilen e-postayı güvenli bir outbox kuyruğunda
/// tutar. SMTP geçici olarak kapalı olsa bile asıl iş işlemi geri alınmaz.
/// </summary>
[Table("email_outbox", Schema = "fault_management")]
public sealed class EmailOutbox
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("user_id")] public long UserId { get; set; }
    [Column("fault_id")] public long? FaultId { get; set; }
    [Column("notification_type"), StringLength(50)] public string NotificationType { get; set; } = null!;
    [Column("recipient_email"), StringLength(254)] public string RecipientEmail { get; set; } = null!;
    [Column("recipient_name"), StringLength(200)] public string RecipientName { get; set; } = null!;
    [Column("subject"), StringLength(300)] public string Subject { get; set; } = null!;
    [Column("html_body")] public string HtmlBody { get; set; } = null!;
    [Column("status"), StringLength(20)] public string Status { get; set; } = "PENDING";
    [Column("retry_count")] public int RetryCount { get; set; }
    [Column("next_retry_at")] public DateTime? NextRetryAt { get; set; }
    [Column("last_error"), StringLength(2000)] public string? LastError { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("sent_at")] public DateTime? SentAt { get; set; }
}

