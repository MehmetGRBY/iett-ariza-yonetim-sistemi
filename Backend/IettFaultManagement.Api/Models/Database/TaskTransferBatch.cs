using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("task_transfer_batches", Schema = "fault_management")]
[Index("FaultId", "TransferredAt", Name = "ix_task_transfer_batches_fault", IsDescending = new[] { false, true })]
public partial class TaskTransferBatch
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("fault_id")]
    public long FaultId { get; set; }

    [Column("old_vehicle_id")]
    public long OldVehicleId { get; set; }

    [Column("new_vehicle_id")]
    public long NewVehicleId { get; set; }

    [Column("driver_id")]
    public long? DriverId { get; set; }

    [Column("garage_id")]
    public long GarageId { get; set; }

    [Column("transfer_type")]
    [StringLength(20)]
    public string TransferType { get; set; } = null!;

    [Column("transferred_task_count")]
    public int TransferredTaskCount { get; set; }

    [Column("driver_can_continue")]
    public bool DriverCanContinue { get; set; }

    [Column("is_automatic")]
    public bool IsAutomatic { get; set; }

    [Column("transferred_by_user_id")]
    public long? TransferredByUserId { get; set; }

    [Column("transferred_at")]
    public DateTime TransferredAt { get; set; }

    [Column("description")]
    [StringLength(1000)]
    public string Description { get; set; } = null!;

    [Column("service_duty_id")]
    public long? ServiceDutyId { get; set; }

    [ForeignKey("DriverId")]
    [InverseProperty("TaskTransferBatches")]
    public virtual Driver? Driver { get; set; }

    [ForeignKey("FaultId")]
    [InverseProperty("TaskTransferBatches")]
    public virtual Fault Fault { get; set; } = null!;

    [ForeignKey("GarageId")]
    [InverseProperty("TaskTransferBatches")]
    public virtual Garage Garage { get; set; } = null!;

    [ForeignKey("NewVehicleId")]
    [InverseProperty("TaskTransferBatchNewVehicles")]
    public virtual Vehicle NewVehicle { get; set; } = null!;

    [InverseProperty("TaskTransferBatch")]
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    [ForeignKey("OldVehicleId")]
    [InverseProperty("TaskTransferBatchOldVehicles")]
    public virtual Vehicle OldVehicle { get; set; } = null!;

    [ForeignKey("ServiceDutyId")]
    [InverseProperty("TaskTransferBatches")]
    public virtual ServiceDuty? ServiceDuty { get; set; }

    [InverseProperty("TransferBatch")]
    public virtual ICollection<TaskAssignment> TaskAssignments { get; set; } = new List<TaskAssignment>();

    [ForeignKey("TransferredByUserId")]
    [InverseProperty("TaskTransferBatches")]
    public virtual AppUser? TransferredByUser { get; set; }

    [InverseProperty("TransferBatch")]
    public virtual ICollection<VehicleDeliveryAssignment> VehicleDeliveryAssignments { get; set; } = new List<VehicleDeliveryAssignment>();
}
