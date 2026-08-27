using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Keyless]
public partial class VwServiceDutySummary
{
    [Column("service_duty_id")]
    public long? ServiceDutyId { get; set; }

    [Column("duty_number")]
    [StringLength(50)]
    public string? DutyNumber { get; set; }

    [Column("service_date")]
    public DateOnly? ServiceDate { get; set; }

    [Column("duty_status")]
    [StringLength(30)]
    public string? DutyStatus { get; set; }

    [Column("garage_id")]
    public long? GarageId { get; set; }

    [Column("garage_code")]
    [StringLength(30)]
    public string? GarageCode { get; set; }

    [Column("garage_name")]
    [StringLength(150)]
    public string? GarageName { get; set; }

    [Column("route_id")]
    public long? RouteId { get; set; }

    [Column("route_code")]
    [StringLength(30)]
    public string? RouteCode { get; set; }

    [Column("route_name")]
    [StringLength(200)]
    public string? RouteName { get; set; }

    [Column("original_vehicle_id")]
    public long? OriginalVehicleId { get; set; }

    [Column("original_vehicle_door_number")]
    [StringLength(30)]
    public string? OriginalVehicleDoorNumber { get; set; }

    [Column("original_driver_id")]
    public long? OriginalDriverId { get; set; }

    [Column("original_driver_personnel_number")]
    [StringLength(30)]
    public string? OriginalDriverPersonnelNumber { get; set; }

    [Column("original_driver_name")]
    public string? OriginalDriverName { get; set; }

    [Column("total_task_count")]
    public long? TotalTaskCount { get; set; }

    [Column("completed_task_count")]
    public long? CompletedTaskCount { get; set; }

    [Column("remaining_task_count")]
    public long? RemainingTaskCount { get; set; }

    [Column("transfer_pending_count")]
    public long? TransferPendingCount { get; set; }

    [Column("first_planned_departure")]
    public DateTime? FirstPlannedDeparture { get; set; }

    [Column("last_planned_arrival")]
    public DateTime? LastPlannedArrival { get; set; }
}
