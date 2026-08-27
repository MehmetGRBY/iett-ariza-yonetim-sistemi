using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("vehicle_event_logs", Schema = "fault_management")]
[Index("VehicleId", "OccurredAt", Name = "ix_vehicle_event_logs_vehicle_date", IsDescending = new[] { false, true })]
public partial class VehicleEventLog
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("vehicle_id")]
    public long VehicleId { get; set; }

    [Column("fault_id")]
    public long? FaultId { get; set; }

    [Column("service_task_id")]
    public long? ServiceTaskId { get; set; }

    [Column("event_type")]
    [StringLength(50)]
    public string EventType { get; set; } = null!;

    [Column("title")]
    [StringLength(200)]
    public string Title { get; set; } = null!;

    [Column("description")]
    [StringLength(1000)]
    public string? Description { get; set; }

    [Column("old_values", TypeName = "jsonb")]
    public string? OldValues { get; set; }

    [Column("new_values", TypeName = "jsonb")]
    public string? NewValues { get; set; }

    [Column("performed_by_user_id")]
    public long? PerformedByUserId { get; set; }

    [Column("is_system_action")]
    public bool IsSystemAction { get; set; }

    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("FaultId")]
    [InverseProperty("VehicleEventLogs")]
    public virtual Fault? Fault { get; set; }

    [ForeignKey("PerformedByUserId")]
    [InverseProperty("VehicleEventLogs")]
    public virtual AppUser? PerformedByUser { get; set; }

    [ForeignKey("ServiceTaskId")]
    [InverseProperty("VehicleEventLogs")]
    public virtual ServiceTask? ServiceTask { get; set; }

    [ForeignKey("VehicleId")]
    [InverseProperty("VehicleEventLogs")]
    public virtual Vehicle Vehicle { get; set; } = null!;
}
