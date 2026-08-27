using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("faults", Schema = "fault_management")]
[Index("FaultNumber", Name = "faults_fault_number_key", IsUnique = true)]
[Index("FaultCategoryId", Name = "ix_faults_category_id")]
[Index("DriverId", Name = "ix_faults_driver_id")]
[Index("OccurredAt", Name = "ix_faults_occurred_at", AllDescending = true)]
[Index("VehicleId", "CreatedAt", Name = "ix_faults_vehicle_created", IsDescending = new[] { false, true })]
[Index("VehicleId", Name = "ix_faults_vehicle_id")]
/// <summary>Araç, sürücü, garaj, kategori, durum, SLA, atama, rapor ve geçmişi bağlayan ana arıza entity'sidir.</summary>
public partial class Fault
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("fault_number")]
    [StringLength(40)]
    public string FaultNumber { get; set; } = null!;

    [Column("vehicle_id")]
    public long VehicleId { get; set; }

    [Column("driver_id")]
    // Garaj/servis öncesi kontrolde arıza sürücüsüz tespit edilebildiği için nullable'dır.
    public long? DriverId { get; set; }

    [Column("created_by_user_id")]
    public long CreatedByUserId { get; set; }

    [Column("garage_id")]
    public long GarageId { get; set; }

    [Column("fault_category_id")]
    public long FaultCategoryId { get; set; }

    [Column("fault_status_id")]
    public long FaultStatusId { get; set; }

    [Column("description")]
    public string Description { get; set; } = null!;

    [Column("mileage_at_failure")]
    public int MileageAtFailure { get; set; }

    [Column("latitude")]
    [Precision(9, 6)]
    public decimal Latitude { get; set; }

    [Column("longitude")]
    [Precision(9, 6)]
    public decimal Longitude { get; set; }

    [Column("location_description")]
    [StringLength(500)]
    public string? LocationDescription { get; set; }

    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("closed_at")]
    public DateTime? ClosedAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("deactivated_at")]
    public DateTime? DeactivatedAt { get; set; }

    [Column("deactivated_by_user_id")]
    public long? DeactivatedByUserId { get; set; }

    [Column("deactivation_reason")]
    [StringLength(500)]
    public string? DeactivationReason { get; set; }

    [Column("service_task_id")]
    public long? ServiceTaskId { get; set; }

    [ForeignKey("CreatedByUserId")]
    [InverseProperty("FaultCreatedByUsers")]
    public virtual AppUser CreatedByUser { get; set; } = null!;

    [ForeignKey("DeactivatedByUserId")]
    [InverseProperty("FaultDeactivatedByUsers")]
    public virtual AppUser? DeactivatedByUser { get; set; }

    [ForeignKey("DriverId")]
    [InverseProperty("Faults")]
    public virtual Driver? Driver { get; set; }

    [InverseProperty("Fault")]
    public virtual ICollection<FaultAlert> FaultAlerts { get; set; } = new List<FaultAlert>();

    // Bir kontrol başarısız olduğunda yeni bir tamir denemesi ve ekip ataması oluşabilir.
    // Bu nedenle arıza-atama ilişkisi bire bir değil, geçmişi koruyan bire çok ilişkidir.
    [InverseProperty("Fault")]
    public virtual ICollection<FaultAssignment> FaultAssignments { get; set; } = new List<FaultAssignment>();

    [InverseProperty("Fault")]
    public virtual ICollection<FaultAttachment> FaultAttachments { get; set; } = new List<FaultAttachment>();

    [ForeignKey("FaultCategoryId")]
    [InverseProperty("Faults")]
    public virtual FaultCategory FaultCategory { get; set; } = null!;

    [ForeignKey("FaultStatusId")]
    [InverseProperty("Faults")]
    public virtual FaultStatus FaultStatus { get; set; } = null!;

    [InverseProperty("Fault")]
    public virtual ICollection<FaultStatusHistory> FaultStatusHistories { get; set; } = new List<FaultStatusHistory>();

    [ForeignKey("GarageId")]
    [InverseProperty("Faults")]
    public virtual Garage Garage { get; set; } = null!;

    [InverseProperty("Fault")]
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    [ForeignKey("ServiceTaskId")]
    [InverseProperty("Faults")]
    public virtual ServiceTask? ServiceTask { get; set; }

    [InverseProperty("Fault")]
    public virtual ICollection<TaskTransferBatch> TaskTransferBatches { get; set; } = new List<TaskTransferBatch>();

    [ForeignKey("VehicleId")]
    [InverseProperty("Faults")]
    public virtual Vehicle Vehicle { get; set; } = null!;

    [InverseProperty("Fault")]
    public virtual VehicleDeliveryAssignment? VehicleDeliveryAssignment { get; set; }

    [InverseProperty("Fault")]
    public virtual ICollection<VehicleEventLog> VehicleEventLogs { get; set; } = new List<VehicleEventLog>();

    [InverseProperty("Fault")]
    public virtual ICollection<VehicleStatusHistory> VehicleStatusHistories { get; set; } = new List<VehicleStatusHistory>();
}
