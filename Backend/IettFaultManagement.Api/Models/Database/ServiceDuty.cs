using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("service_duties", Schema = "fault_management")]
[Index("DutyNumber", Name = "service_duties_duty_number_key", IsUnique = true)]
public partial class ServiceDuty
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("duty_number")]
    [StringLength(50)]
    public string DutyNumber { get; set; } = null!;

    [Column("service_date")]
    public DateOnly ServiceDate { get; set; }

    [Column("garage_id")]
    public long GarageId { get; set; }

    [Column("route_id")]
    public long RouteId { get; set; }

    [Column("original_vehicle_id")]
    public long? OriginalVehicleId { get; set; }

    [Column("original_driver_id")]
    public long? OriginalDriverId { get; set; }

    [Column("status")]
    [StringLength(30)]
    public string Status { get; set; } = null!;

    [Column("description")]
    [StringLength(1000)]
    public string? Description { get; set; }

    [Column("created_by_user_id")]
    public long CreatedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("deactivated_at")]
    public DateTime? DeactivatedAt { get; set; }

    [Column("deactivated_by_user_id")]
    public long? DeactivatedByUserId { get; set; }

    [Column("deactivation_reason")]
    [StringLength(500)]
    public string? DeactivationReason { get; set; }

    [ForeignKey("CreatedByUserId")]
    [InverseProperty("ServiceDutyCreatedByUsers")]
    public virtual AppUser CreatedByUser { get; set; } = null!;

    [ForeignKey("DeactivatedByUserId")]
    [InverseProperty("ServiceDutyDeactivatedByUsers")]
    public virtual AppUser? DeactivatedByUser { get; set; }

    [ForeignKey("GarageId")]
    [InverseProperty("ServiceDuties")]
    public virtual Garage Garage { get; set; } = null!;

    [ForeignKey("OriginalDriverId")]
    [InverseProperty("ServiceDuties")]
    public virtual Driver? OriginalDriver { get; set; }

    [ForeignKey("OriginalVehicleId")]
    [InverseProperty("ServiceDuties")]
    public virtual Vehicle? OriginalVehicle { get; set; }

    [ForeignKey("RouteId")]
    [InverseProperty("ServiceDuties")]
    public virtual Route Route { get; set; } = null!;

    [InverseProperty("ServiceDuty")]
    public virtual ICollection<ServiceTask> ServiceTasks { get; set; } = new List<ServiceTask>();

    [InverseProperty("ServiceDuty")]
    public virtual ICollection<TaskTransferBatch> TaskTransferBatches { get; set; } = new List<TaskTransferBatch>();
}
