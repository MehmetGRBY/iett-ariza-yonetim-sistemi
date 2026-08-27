using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Keyless]
public partial class VwTeamWorkload
{
    [Column("team_id")]
    public long? TeamId { get; set; }

    [Column("team_name")]
    [StringLength(120)]
    public string? TeamName { get; set; }

    [Column("garage_id")]
    public long? GarageId { get; set; }

    [Column("garage_name")]
    [StringLength(150)]
    public string? GarageName { get; set; }

    [Column("is_available")]
    public bool? IsAvailable { get; set; }

    [Column("active_assignment_count")]
    public long? ActiveAssignmentCount { get; set; }

    [Column("total_assignment_count")]
    public long? TotalAssignmentCount { get; set; }

    [Column("last_assignment_at")]
    public DateTime? LastAssignmentAt { get; set; }
}
