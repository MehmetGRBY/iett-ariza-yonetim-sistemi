using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("service_tasks", Schema = "fault_management")]
[Index("RouteId", "ServiceDate", "SequenceNumber", Name = "ix_service_tasks_route_date")]
[Index("TaskNumber", Name = "service_tasks_task_number_key", IsUnique = true)]
[Index("ServiceDutyId", "SequenceNumber", Name = "uq_service_tasks_duty_sequence", IsUnique = true)]
/// <summary>Bir hat ve vardiya içindeki planlı gidiş-dönüş sefer görevini ve zaman aralığını temsil eder.</summary>
public partial class ServiceTask
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("task_number")]
    [StringLength(50)]
    public string TaskNumber { get; set; } = null!;

    [Column("route_id")]
    public long RouteId { get; set; }

    [Column("service_date")]
    public DateOnly ServiceDate { get; set; }

    [Column("sequence_number")]
    public int SequenceNumber { get; set; }

    [Column("planned_departure_at")]
    public DateTime PlannedDepartureAt { get; set; }

    [Column("planned_arrival_at")]
    public DateTime PlannedArrivalAt { get; set; }

    [Column("status")]
    [StringLength(30)]
    public string Status { get; set; } = null!;

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_by_user_id")]
    public long CreatedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("deactivated_at")]
    public DateTime? DeactivatedAt { get; set; }

    [Column("deactivated_by_user_id")]
    public long? DeactivatedByUserId { get; set; }

    [Column("deactivation_reason")]
    [StringLength(500)]
    public string? DeactivationReason { get; set; }

    [Column("actual_departure_at")]
    public DateTime? ActualDepartureAt { get; set; }

    [Column("actual_arrival_at")]
    public DateTime? ActualArrivalAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Column("service_duty_id")]
    public long ServiceDutyId { get; set; }

    [ForeignKey("CreatedByUserId")]
    [InverseProperty("ServiceTaskCreatedByUsers")]
    public virtual AppUser CreatedByUser { get; set; } = null!;

    [ForeignKey("DeactivatedByUserId")]
    [InverseProperty("ServiceTaskDeactivatedByUsers")]
    public virtual AppUser? DeactivatedByUser { get; set; }

    [InverseProperty("ServiceTask")]
    public virtual ICollection<Fault> Faults { get; set; } = new List<Fault>();

    [InverseProperty("ServiceTask")]
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    [ForeignKey("RouteId")]
    [InverseProperty("ServiceTasks")]
    public virtual Route Route { get; set; } = null!;

    [ForeignKey("ServiceDutyId")]
    [InverseProperty("ServiceTasks")]
    public virtual ServiceDuty ServiceDuty { get; set; } = null!;

    [InverseProperty("ServiceTask")]
    public virtual ICollection<TaskAssignment> TaskAssignments { get; set; } = new List<TaskAssignment>();

    [InverseProperty("ServiceTask")]
    public virtual ICollection<VehicleEventLog> VehicleEventLogs { get; set; } = new List<VehicleEventLog>();
}
