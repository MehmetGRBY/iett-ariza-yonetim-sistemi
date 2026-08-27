using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Keyless]
public partial class VwAvailableDriver
{
    [Column("driver_id")]
    public long? DriverId { get; set; }

    [Column("personnel_number")]
    [StringLength(30)]
    public string? PersonnelNumber { get; set; }

    [Column("first_name")]
    [StringLength(100)]
    public string? FirstName { get; set; }

    [Column("last_name")]
    [StringLength(100)]
    public string? LastName { get; set; }

    [Column("driver_type")]
    [StringLength(20)]
    public string? DriverType { get; set; }

    [Column("garage_id")]
    public long? GarageId { get; set; }

    [Column("garage_code")]
    [StringLength(30)]
    public string? GarageCode { get; set; }

    [Column("garage_name")]
    [StringLength(150)]
    public string? GarageName { get; set; }

    [Column("authorized_vehicle_types")]
    public string? AuthorizedVehicleTypes { get; set; }
}
