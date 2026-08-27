using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("task_assignments", Schema = "fault_management")]
/// <summary>Bir servis görevine atanan araç ve sürücüyü; atama zamanı, türü ve açıklamasıyla kaydeder.</summary>
public partial class TaskAssignment
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("service_task_id")]
    public long ServiceTaskId { get; set; }

    [Column("vehicle_id")]
    public long VehicleId { get; set; }

    [Column("driver_id")]
    public long DriverId { get; set; }

    [Column("transfer_batch_id")]
    public long? TransferBatchId { get; set; }

    [Column("assignment_type")]
    [StringLength(20)]
    public string AssignmentType { get; set; } = null!;

    [Column("assigned_by_user_id")]
    public long? AssignedByUserId { get; set; }

    [Column("assigned_at")]
    public DateTime AssignedAt { get; set; }

    [Column("ended_at")]
    public DateTime? EndedAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("description")]
    [StringLength(1000)]
    public string? Description { get; set; }

    [ForeignKey("AssignedByUserId")]
    [InverseProperty("TaskAssignments")]
    public virtual AppUser? AssignedByUser { get; set; }

    [ForeignKey("DriverId")]
    [InverseProperty("TaskAssignments")]
    public virtual Driver Driver { get; set; } = null!;

    [ForeignKey("ServiceTaskId")]
    [InverseProperty("TaskAssignments")]
    public virtual ServiceTask ServiceTask { get; set; } = null!;

    [ForeignKey("TransferBatchId")]
    [InverseProperty("TaskAssignments")]
    public virtual TaskTransferBatch? TransferBatch { get; set; }

    [ForeignKey("VehicleId")]
    [InverseProperty("TaskAssignments")]
    public virtual Vehicle Vehicle { get; set; } = null!;
}
