using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Keyless]
public partial class VwFaultResolutionTime
{
    [Column("fault_id")]
    public long? FaultId { get; set; }

    [Column("fault_number")]
    [StringLength(40)]
    public string? FaultNumber { get; set; }

    [Column("vehicle_id")]
    public long? VehicleId { get; set; }

    [Column("door_number")]
    [StringLength(30)]
    public string? DoorNumber { get; set; }

    [Column("garage_id")]
    public long? GarageId { get; set; }

    [Column("garage_name")]
    [StringLength(150)]
    public string? GarageName { get; set; }

    [Column("occurred_at")]
    public DateTime? OccurredAt { get; set; }

    [Column("closed_at")]
    public DateTime? ClosedAt { get; set; }

    [Column("fault_duration")]
    public TimeSpan? FaultDuration { get; set; }

    [Column("is_closed")]
    public bool? IsClosed { get; set; }
}
