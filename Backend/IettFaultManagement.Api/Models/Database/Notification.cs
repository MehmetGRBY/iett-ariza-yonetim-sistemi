using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("notifications", Schema = "fault_management")]
public partial class Notification
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("user_id")]
    public long UserId { get; set; }

    [Column("fault_id")]
    public long? FaultId { get; set; }

    [Column("title")]
    [StringLength(200)]
    public string Title { get; set; } = null!;

    [Column("message")]
    [StringLength(1000)]
    public string Message { get; set; } = null!;

    [Column("is_read")]
    public bool IsRead { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("read_at")]
    public DateTime? ReadAt { get; set; }

    [Column("service_task_id")]
    public long? ServiceTaskId { get; set; }

    [Column("task_transfer_batch_id")]
    public long? TaskTransferBatchId { get; set; }

    [Column("notification_type")]
    [StringLength(50)]
    public string NotificationType { get; set; } = null!;

    [ForeignKey("FaultId")]
    [InverseProperty("Notifications")]
    public virtual Fault? Fault { get; set; }

    [ForeignKey("ServiceTaskId")]
    [InverseProperty("Notifications")]
    public virtual ServiceTask? ServiceTask { get; set; }

    [ForeignKey("TaskTransferBatchId")]
    [InverseProperty("Notifications")]
    public virtual TaskTransferBatch? TaskTransferBatch { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("Notifications")]
    public virtual AppUser User { get; set; } = null!;
}
