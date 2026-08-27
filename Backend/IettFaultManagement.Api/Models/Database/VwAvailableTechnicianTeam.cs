using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Keyless]
public partial class VwAvailableTechnicianTeam
{
    [Column("team_id")]
    public long? TeamId { get; set; }

    [Column("team_name")]
    [StringLength(120)]
    public string? TeamName { get; set; }

    [Column("garage_id")]
    public long? GarageId { get; set; }

    [Column("garage_code")]
    [StringLength(30)]
    public string? GarageCode { get; set; }

    [Column("garage_name")]
    [StringLength(150)]
    public string? GarageName { get; set; }

    [Column("active_member_count")]
    public long? ActiveMemberCount { get; set; }

    [Column("active_leader_count")]
    public long? ActiveLeaderCount { get; set; }

    [Column("last_assigned_at")]
    public DateTime? LastAssignedAt { get; set; }
}
