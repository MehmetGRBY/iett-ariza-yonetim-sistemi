using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Keyless]
public partial class VwAvailableVehicle
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

    [Column("model_year")]
    public short? ModelYear { get; set; }

    [Column("vehicle_type_id")]
    public long? VehicleTypeId { get; set; }

    [Column("vehicle_type_name")]
    [StringLength(80)]
    public string? VehicleTypeName { get; set; }

    [Column("capacity")]
    public int? Capacity { get; set; }

    [Column("current_mileage")]
    public int? CurrentMileage { get; set; }

    [Column("garage_id")]
    public long? GarageId { get; set; }

    [Column("garage_code")]
    [StringLength(30)]
    public string? GarageCode { get; set; }

    [Column("garage_name")]
    [StringLength(150)]
    public string? GarageName { get; set; }

    [Column("vehicle_status_code")]
    [StringLength(50)]
    public string? VehicleStatusCode { get; set; }

    [Column("vehicle_status_name")]
    [StringLength(80)]
    public string? VehicleStatusName { get; set; }
}
