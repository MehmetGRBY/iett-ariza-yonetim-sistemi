using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("vehicle_delivery_assignments", Schema = "fault_management")]
[Index("ReplacementVehicleId", "CreatedAt", Name = "ix_vehicle_delivery_replacement_vehicle", IsDescending = new[] { false, true })]
[Index("DeliveryNumber", Name = "vehicle_delivery_assignments_delivery_number_key", IsUnique = true)]
public partial class VehicleDeliveryAssignment
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("delivery_number")]
    [StringLength(50)]
    public string DeliveryNumber { get; set; } = null!;

    [Column("fault_id")]
    public long FaultId { get; set; }

    [Column("transfer_batch_id")]
    public long? TransferBatchId { get; set; }

    [Column("garage_id")]
    public long GarageId { get; set; }

    [Column("broken_vehicle_id")]
    public long BrokenVehicleId { get; set; }

    [Column("replacement_vehicle_id")]
    public long ReplacementVehicleId { get; set; }

    [Column("support_vehicle_id")]
    public long? SupportVehicleId { get; set; }

    [Column("delivery_driver_id")]
    public long DeliveryDriverId { get; set; }

    [Column("receiving_driver_id")]
    public long? ReceivingDriverId { get; set; }

    [Column("delivery_mode")]
    [StringLength(30)]
    public string DeliveryMode { get; set; } = null!;

    [Column("delivery_status")]
    [StringLength(30)]
    public string DeliveryStatus { get; set; } = null!;

    [Column("planned_at")]
    public DateTime PlannedAt { get; set; }

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("arrived_at")]
    public DateTime? ArrivedAt { get; set; }

    [Column("handed_over_at")]
    public DateTime? HandedOverAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Column("created_by_user_id")]
    public long CreatedByUserId { get; set; }

    [Column("completed_by_user_id")]
    public long? CompletedByUserId { get; set; }

    [Column("description")]
    [StringLength(1000)]
    public string Description { get; set; } = null!;

    [Column("completion_note")]
    [StringLength(1000)]
    public string? CompletionNote { get; set; }

    [Column("is_automatic")]
    public bool IsAutomatic { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("BrokenVehicleId")]
    [InverseProperty("VehicleDeliveryAssignmentBrokenVehicles")]
    public virtual Vehicle BrokenVehicle { get; set; } = null!;

    [ForeignKey("CompletedByUserId")]
    [InverseProperty("VehicleDeliveryAssignmentCompletedByUsers")]
    public virtual AppUser? CompletedByUser { get; set; }

    [ForeignKey("CreatedByUserId")]
    [InverseProperty("VehicleDeliveryAssignmentCreatedByUsers")]
    public virtual AppUser CreatedByUser { get; set; } = null!;

    [ForeignKey("DeliveryDriverId")]
    [InverseProperty("VehicleDeliveryAssignmentDeliveryDrivers")]
    public virtual Driver DeliveryDriver { get; set; } = null!;

    [ForeignKey("FaultId")]
    [InverseProperty("VehicleDeliveryAssignment")]
    public virtual Fault Fault { get; set; } = null!;

    [ForeignKey("GarageId")]
    [InverseProperty("VehicleDeliveryAssignments")]
    public virtual Garage Garage { get; set; } = null!;

    [ForeignKey("ReceivingDriverId")]
    [InverseProperty("VehicleDeliveryAssignmentReceivingDrivers")]
    public virtual Driver? ReceivingDriver { get; set; }

    [ForeignKey("ReplacementVehicleId")]
    [InverseProperty("VehicleDeliveryAssignmentReplacementVehicles")]
    public virtual Vehicle ReplacementVehicle { get; set; } = null!;

    [ForeignKey("SupportVehicleId")]
    [InverseProperty("VehicleDeliveryAssignmentSupportVehicles")]
    public virtual Vehicle? SupportVehicle { get; set; }

    [ForeignKey("TransferBatchId")]
    [InverseProperty("VehicleDeliveryAssignments")]
    public virtual TaskTransferBatch? TransferBatch { get; set; }
}
