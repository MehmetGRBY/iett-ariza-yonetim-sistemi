using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Keyless]
public partial class VwDriverFaultSummary
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

    [Column("driver_is_active")]
    public bool? DriverIsActive { get; set; }

    [Column("total_fault_count")]
    public long? TotalFaultCount { get; set; }

    [Column("open_fault_count")]
    public long? OpenFaultCount { get; set; }

    [Column("closed_fault_count")]
    public long? ClosedFaultCount { get; set; }

    [Column("last_fault_at")]
    public DateTime? LastFaultAt { get; set; }
}
