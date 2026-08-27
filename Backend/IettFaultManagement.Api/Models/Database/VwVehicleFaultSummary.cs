using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Keyless]
public partial class VwVehicleFaultSummary
{
    [Column("vehicle_id")]
    public long? VehicleId { get; set; }

    [Column("door_number")]
    [StringLength(30)]
    public string? DoorNumber { get; set; }

    [Column("plate")]
    [StringLength(20)]
    public string? Plate { get; set; }

    [Column("brand")]
    [StringLength(80)]
    public string? Brand { get; set; }

    [Column("model")]
    [StringLength(100)]
    public string? Model { get; set; }

    [Column("garage_name")]
    [StringLength(150)]
    public string? GarageName { get; set; }

    [Column("total_fault_count")]
    public long? TotalFaultCount { get; set; }

    [Column("open_fault_count")]
    public long? OpenFaultCount { get; set; }

    [Column("closed_fault_count")]
    public long? ClosedFaultCount { get; set; }

    [Column("last_fault_at")]
    public DateTime? LastFaultAt { get; set; }
}
