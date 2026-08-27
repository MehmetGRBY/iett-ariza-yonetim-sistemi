using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Keyless]
public partial class VwTaskTransferSummary
{
    [Column("transfer_batch_id")]
    public long? TransferBatchId { get; set; }

    [Column("fault_id")]
    public long? FaultId { get; set; }

    [Column("fault_number")]
    [StringLength(40)]
    public string? FaultNumber { get; set; }

    [Column("old_vehicle_id")]
    public long? OldVehicleId { get; set; }

    [Column("old_vehicle_door_number")]
    [StringLength(30)]
    public string? OldVehicleDoorNumber { get; set; }

    [Column("new_vehicle_id")]
    public long? NewVehicleId { get; set; }

    [Column("new_vehicle_door_number")]
    [StringLength(30)]
    public string? NewVehicleDoorNumber { get; set; }

    [Column("garage_id")]
    public long? GarageId { get; set; }

    [Column("garage_name")]
    [StringLength(150)]
    public string? GarageName { get; set; }

    [Column("transfer_type")]
    [StringLength(20)]
    public string? TransferType { get; set; }

    [Column("transferred_task_count")]
    public int? TransferredTaskCount { get; set; }

    [Column("is_automatic")]
    public bool? IsAutomatic { get; set; }

    [Column("transferred_at")]
    public DateTime? TransferredAt { get; set; }
}
