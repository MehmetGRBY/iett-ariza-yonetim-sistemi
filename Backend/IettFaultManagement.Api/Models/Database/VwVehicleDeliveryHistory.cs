using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Keyless]
public partial class VwVehicleDeliveryHistory
{
    [Column("delivery_id")]
    public long? DeliveryId { get; set; }

    [Column("delivery_number")]
    [StringLength(50)]
    public string? DeliveryNumber { get; set; }

    [Column("fault_number")]
    [StringLength(40)]
    public string? FaultNumber { get; set; }

    [Column("garage_name")]
    [StringLength(150)]
    public string? GarageName { get; set; }

    [Column("broken_vehicle_door_number")]
    [StringLength(30)]
    public string? BrokenVehicleDoorNumber { get; set; }

    [Column("replacement_vehicle_door_number")]
    [StringLength(30)]
    public string? ReplacementVehicleDoorNumber { get; set; }

    [Column("delivery_driver_personnel_number")]
    [StringLength(30)]
    public string? DeliveryDriverPersonnelNumber { get; set; }

    [Column("delivery_driver_name")]
    public string? DeliveryDriverName { get; set; }

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

    [Column("handed_over_at")]
    public DateTime? HandedOverAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Column("total_delivery_duration")]
    public TimeSpan? TotalDeliveryDuration { get; set; }

    [Column("description")]
    [StringLength(1000)]
    public string? Description { get; set; }

    [Column("completion_note")]
    [StringLength(1000)]
    public string? CompletionNote { get; set; }

    [Column("is_automatic")]
    public bool? IsAutomatic { get; set; }
}
