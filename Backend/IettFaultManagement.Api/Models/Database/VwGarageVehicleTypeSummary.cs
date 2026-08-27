using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Keyless]
public partial class VwGarageVehicleTypeSummary
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

    [Column("vehicle_type_id")]
    public long? VehicleTypeId { get; set; }

    [Column("vehicle_type_name")]
    [StringLength(80)]
    public string? VehicleTypeName { get; set; }

    [Column("active_vehicle_count")]
    public long? ActiveVehicleCount { get; set; }

    [Column("passive_vehicle_count")]
    public long? PassiveVehicleCount { get; set; }

    [Column("total_vehicle_count")]
    public long? TotalVehicleCount { get; set; }
}
