using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("team_members", Schema = "fault_management")]
public partial class TeamMember
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("team_id")]
    public long TeamId { get; set; }

    [Column("user_id")]
    public long UserId { get; set; }

    [Column("is_team_leader")]
    public bool IsTeamLeader { get; set; }

    [Column("joined_at")]
    public DateTime JoinedAt { get; set; }

    [Column("left_at")]
    public DateTime? LeftAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("work_status")]
    [StringLength(20)]
    public string WorkStatus { get; set; } = null!;

    [ForeignKey("TeamId")]
    [InverseProperty("TeamMembers")]
    public virtual TechnicianTeam Team { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("TeamMember")]
    public virtual AppUser User { get; set; } = null!;
}
