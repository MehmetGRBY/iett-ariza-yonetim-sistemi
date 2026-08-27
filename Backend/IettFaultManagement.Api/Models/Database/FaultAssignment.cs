using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("fault_assignments", Schema = "fault_management")]
public partial class FaultAssignment
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("fault_id")]
    public long FaultId { get; set; }

    [Column("team_id")]
    public long TeamId { get; set; }

    [Column("assigned_by_user_id")]
    public long? AssignedByUserId { get; set; }

    [Column("is_automatic")]
    public bool IsAutomatic { get; set; }

    [Column("assigned_at")]
    public DateTime AssignedAt { get; set; }

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [ForeignKey("AssignedByUserId")]
    [InverseProperty("FaultAssignments")]
    public virtual AppUser? AssignedByUser { get; set; }

    [ForeignKey("FaultId")]
    [InverseProperty("FaultAssignments")]
    public virtual Fault Fault { get; set; } = null!;

    [InverseProperty("FaultAssignment")]
    public virtual ICollection<RepairReport> RepairReports { get; set; } = new List<RepairReport>();

    [ForeignKey("TeamId")]
    [InverseProperty("FaultAssignments")]
    public virtual TechnicianTeam Team { get; set; } = null!;
}
