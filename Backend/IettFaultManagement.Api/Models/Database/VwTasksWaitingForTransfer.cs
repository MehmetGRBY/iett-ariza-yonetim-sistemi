using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Keyless]
public partial class VwTasksWaitingForTransfer
{
    [Column("service_task_id")]
    public long? ServiceTaskId { get; set; }

    [Column("task_number")]
    [StringLength(50)]
    public string? TaskNumber { get; set; }

    [Column("service_date")]
    public DateOnly? ServiceDate { get; set; }

    [Column("sequence_number")]
    public int? SequenceNumber { get; set; }

    [Column("planned_departure_at")]
    public DateTime? PlannedDepartureAt { get; set; }

    [Column("planned_arrival_at")]
    public DateTime? PlannedArrivalAt { get; set; }

    [Column("route_id")]
    public long? RouteId { get; set; }

    [Column("route_code")]
    [StringLength(30)]
    public string? RouteCode { get; set; }

    [Column("route_name")]
    [StringLength(200)]
    public string? RouteName { get; set; }

    [Column("current_vehicle_id")]
    public long? CurrentVehicleId { get; set; }

    [Column("current_vehicle_door_number")]
    [StringLength(30)]
    public string? CurrentVehicleDoorNumber { get; set; }

    [Column("garage_id")]
    public long? GarageId { get; set; }

    [Column("garage_name")]
    [StringLength(150)]
    public string? GarageName { get; set; }

    [Column("driver_id")]
    public long? DriverId { get; set; }

    [Column("driver_personnel_number")]
    [StringLength(30)]
    public string? DriverPersonnelNumber { get; set; }

    [Column("driver_full_name")]
    public string? DriverFullName { get; set; }

    [Column("fault_id")]
    public long? FaultId { get; set; }

    [Column("fault_number")]
    [StringLength(40)]
    public string? FaultNumber { get; set; }
}
