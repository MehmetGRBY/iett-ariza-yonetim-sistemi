using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Keyless]
public partial class VwGarageOccupancy
{
    [Column("garage_id")]
    public long? GarageId { get; set; }

    [Column("garage_code")]
    [StringLength(30)]
    public string? GarageCode { get; set; }

    [Column("garage_name")]
    [StringLength(150)]
    public string? GarageName { get; set; }

    [Column("vehicle_capacity")]
    public int? VehicleCapacity { get; set; }

    [Column("active_vehicle_count")]
    public long? ActiveVehicleCount { get; set; }

    [Column("remaining_capacity")]
    public long? RemainingCapacity { get; set; }

    [Column("occupancy_rate")]
    public decimal? OccupancyRate { get; set; }
}
