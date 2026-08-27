using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("vehicles", Schema = "fault_management")]
[Index("Brand", "Model", Name = "ix_vehicles_brand_model")]
[Index("DoorNumber", Name = "vehicles_door_number_key", IsUnique = true)]
[Index("Plate", Name = "vehicles_plate_key", IsUnique = true)]
/// <summary>
/// vehicles tablosunun EF Core karşılığıdır. Attribute'lar kolon/index eşlemesini,
/// navigation alanları ise garaj, tip, durum, arıza ve geçmiş ilişkilerini tanımlar.
/// </summary>
public partial class Vehicle
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("door_number")]
    [StringLength(30)]
    public string DoorNumber { get; set; } = null!;

    [Column("plate")]
    [StringLength(20)]
    public string Plate { get; set; } = null!;

    [Column("brand")]
    [StringLength(80)]
    public string Brand { get; set; } = null!;

    [Column("model")]
    [StringLength(100)]
    public string Model { get; set; } = null!;

    [Column("model_year")]
    public short ModelYear { get; set; }

    [Column("vehicle_type_id")]
    public long VehicleTypeId { get; set; }

    [Column("fuel_type_id")]
    public long FuelTypeId { get; set; }

    [Column("current_mileage")]
    public int CurrentMileage { get; set; }

    [Column("garage_id")]
    public long GarageId { get; set; }

    [Column("vehicle_status_id")]
    public long VehicleStatusId { get; set; }

    [Column("duty_type")]
    [StringLength(100)]
    public string? DutyType { get; set; }

    [Column("capacity")]
    public int? Capacity { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("deactivated_at")]
    public DateTime? DeactivatedAt { get; set; }

    [Column("deactivation_reason")]
    [StringLength(500)]
    public string? DeactivationReason { get; set; }

    [InverseProperty("Vehicle")]
    public virtual ICollection<Fault> Faults { get; set; } = new List<Fault>();

    [ForeignKey("FuelTypeId")]
    [InverseProperty("Vehicles")]
    public virtual FuelType FuelType { get; set; } = null!;

    [ForeignKey("GarageId")]
    [InverseProperty("Vehicles")]
    public virtual Garage Garage { get; set; } = null!;

    [InverseProperty("OriginalVehicle")]
    public virtual ICollection<ServiceDuty> ServiceDuties { get; set; } = new List<ServiceDuty>();

    [InverseProperty("Vehicle")]
    public virtual ICollection<TaskAssignment> TaskAssignments { get; set; } = new List<TaskAssignment>();

    [InverseProperty("NewVehicle")]
    public virtual ICollection<TaskTransferBatch> TaskTransferBatchNewVehicles { get; set; } = new List<TaskTransferBatch>();

    [InverseProperty("OldVehicle")]
    public virtual ICollection<TaskTransferBatch> TaskTransferBatchOldVehicles { get; set; } = new List<TaskTransferBatch>();

    [InverseProperty("BrokenVehicle")]
    public virtual ICollection<VehicleDeliveryAssignment> VehicleDeliveryAssignmentBrokenVehicles { get; set; } = new List<VehicleDeliveryAssignment>();

    [InverseProperty("ReplacementVehicle")]
    public virtual ICollection<VehicleDeliveryAssignment> VehicleDeliveryAssignmentReplacementVehicles { get; set; } = new List<VehicleDeliveryAssignment>();

    [InverseProperty("SupportVehicle")]
    public virtual ICollection<VehicleDeliveryAssignment> VehicleDeliveryAssignmentSupportVehicles { get; set; } = new List<VehicleDeliveryAssignment>();

    [InverseProperty("Vehicle")]
    public virtual ICollection<VehicleEventLog> VehicleEventLogs { get; set; } = new List<VehicleEventLog>();

    [InverseProperty("Vehicle")]
    public virtual ICollection<VehicleGarageHistory> VehicleGarageHistories { get; set; } = new List<VehicleGarageHistory>();

    [ForeignKey("VehicleStatusId")]
    [InverseProperty("Vehicles")]
    public virtual VehicleStatus VehicleStatus { get; set; } = null!;

    [InverseProperty("Vehicle")]
    public virtual ICollection<VehicleStatusHistory> VehicleStatusHistories { get; set; } = new List<VehicleStatusHistory>();

    [ForeignKey("VehicleTypeId")]
    [InverseProperty("Vehicles")]
    public virtual VehicleType VehicleType { get; set; } = null!;
}
