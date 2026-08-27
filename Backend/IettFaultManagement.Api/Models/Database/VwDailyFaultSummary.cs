using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Keyless]
public partial class VwDailyFaultSummary
{
    [Column("fault_date")]
    public DateOnly? FaultDate { get; set; }

    [Column("opened_fault_count")]
    public long? OpenedFaultCount { get; set; }

    [Column("closed_fault_count")]
    public long? ClosedFaultCount { get; set; }

    [Column("still_open_fault_count")]
    public long? StillOpenFaultCount { get; set; }

    [Column("affected_vehicle_count")]
    public long? AffectedVehicleCount { get; set; }

    [Column("average_resolution_hours")]
    public decimal? AverageResolutionHours { get; set; }
}
