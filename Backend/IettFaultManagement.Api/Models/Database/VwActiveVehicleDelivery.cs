using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Keyless]
public partial class VwActiveVehicleDelivery
{
    [Column("delivery_id")]
    public long? DeliveryId { get; set; }

    [Column("delivery_number")]
    [StringLength(50)]
    public string? DeliveryNumber { get; set; }

    [Column("fault_id")]
    public long? FaultId { get; set; }

    [Column("fault_number")]
    [StringLength(40)]
    public string? FaultNumber { get; set; }

    [Column("transfer_batch_id")]
    public long? TransferBatchId { get; set; }

    [Column("garage_id")]
    public long? GarageId { get; set; }

    [Column("garage_code")]
    [StringLength(30)]
    public string? GarageCode { get; set; }

    [Column("garage_name")]
    [StringLength(150)]
    public string? GarageName { get; set; }

    [Column("broken_vehicle_id")]
    public long? BrokenVehicleId { get; set; }

    [Column("broken_vehicle_door_number")]
    [StringLength(30)]
    public string? BrokenVehicleDoorNumber { get; set; }

    [Column("broken_vehicle_plate")]
    [StringLength(20)]
    public string? BrokenVehiclePlate { get; set; }

    [Column("replacement_vehicle_id")]
    public long? ReplacementVehicleId { get; set; }

    [Column("replacement_vehicle_door_number")]
    [StringLength(30)]
    public string? ReplacementVehicleDoorNumber { get; set; }

    [Column("replacement_vehicle_plate")]
    [StringLength(20)]
    public string? ReplacementVehiclePlate { get; set; }

    [Column("support_vehicle_id")]
    public long? SupportVehicleId { get; set; }

    [Column("support_vehicle_door_number")]
    [StringLength(30)]
    public string? SupportVehicleDoorNumber { get; set; }

    [Column("support_vehicle_plate")]
    [StringLength(20)]
    public string? SupportVehiclePlate { get; set; }

    [Column("delivery_driver_id")]
    public long? DeliveryDriverId { get; set; }

    [Column("delivery_driver_personnel_number")]
    [StringLength(30)]
    public string? DeliveryDriverPersonnelNumber { get; set; }

    [Column("delivery_driver_name")]
    public string? DeliveryDriverName { get; set; }

    [Column("receiving_driver_id")]
    public long? ReceivingDriverId { get; set; }

    [Column("receiving_driver_personnel_number")]
    [StringLength(30)]
    public string? ReceivingDriverPersonnelNumber { get; set; }

    [Column("receiving_driver_name")]
    public string? ReceivingDriverName { get; set; }

    [Column("delivery_mode")]
    [StringLength(30)]
    public string? DeliveryMode { get; set; }

    [Column("delivery_status")]
    [StringLength(30)]
    public string? DeliveryStatus { get; set; }

    [Column("planned_at")]
    public DateTime? PlannedAt { get; set; }

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("arrived_at")]
    public DateTime? ArrivedAt { get; set; }

    [Column("description")]
    [StringLength(1000)]
    public string? Description { get; set; }

    [Column("is_automatic")]
    public bool? IsAutomatic { get; set; }

    [Column("elapsed_time")]
    public TimeSpan? ElapsedTime { get; set; }
}
