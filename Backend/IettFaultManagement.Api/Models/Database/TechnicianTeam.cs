using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("technician_teams", Schema = "fault_management")]
[Index("GarageId", "Name", Name = "technician_teams_garage_id_name_key", IsUnique = true)]
public partial class TechnicianTeam
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    [StringLength(120)]
    public string Name { get; set; } = null!;

    [Column("garage_id")]
    public long GarageId { get; set; }

    [Column("is_available")]
    public bool IsAvailable { get; set; }

    [Column("last_assigned_at")]
    public DateTime? LastAssignedAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("Team")]
    public virtual ICollection<FaultAssignment> FaultAssignments { get; set; } = new List<FaultAssignment>();

    [ForeignKey("GarageId")]
    [InverseProperty("TechnicianTeams")]
    public virtual Garage Garage { get; set; } = null!;

    [InverseProperty("Team")]
    public virtual ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
}
