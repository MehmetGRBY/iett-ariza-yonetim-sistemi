using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Keyless]
public partial class VwActiveFault
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

    [Column("plate")]
    [StringLength(20)]
    public string? Plate { get; set; }

    [Column("brand")]
    [StringLength(80)]
    public string? Brand { get; set; }

    [Column("model")]
    [StringLength(100)]
    public string? Model { get; set; }

    [Column("driver_id")]
    public long? DriverId { get; set; }

    [Column("driver_personnel_number")]
    [StringLength(30)]
    public string? DriverPersonnelNumber { get; set; }

    [Column("driver_full_name")]
    public string? DriverFullName { get; set; }

    [Column("garage_id")]
    public long? GarageId { get; set; }

    [Column("garage_code")]
    [StringLength(30)]
    public string? GarageCode { get; set; }

    [Column("garage_name")]
    [StringLength(150)]
    public string? GarageName { get; set; }

    [Column("category_id")]
    public long? CategoryId { get; set; }

    [Column("category_name")]
    [StringLength(120)]
    public string? CategoryName { get; set; }

    [Column("parent_category_id")]
    public long? ParentCategoryId { get; set; }

    [Column("parent_category_name")]
    [StringLength(120)]
    public string? ParentCategoryName { get; set; }

    [Column("fault_status_id")]
    public long? FaultStatusId { get; set; }

    [Column("fault_status_code")]
    [StringLength(50)]
    public string? FaultStatusCode { get; set; }

    [Column("fault_status_name")]
    [StringLength(80)]
    public string? FaultStatusName { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("mileage_at_failure")]
    public int? MileageAtFailure { get; set; }

    [Column("latitude")]
    [Precision(9, 6)]
    public decimal? Latitude { get; set; }

    [Column("longitude")]
    [Precision(9, 6)]
    public decimal? Longitude { get; set; }

    [Column("location_description")]
    [StringLength(500)]
    public string? LocationDescription { get; set; }

    [Column("occurred_at")]
    public DateTime? OccurredAt { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("open_duration")]
    public TimeSpan? OpenDuration { get; set; }
}
