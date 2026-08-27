using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("repair_reports", Schema = "fault_management")]
public partial class RepairReport
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("fault_assignment_id")]
    public long FaultAssignmentId { get; set; }

    [Column("created_by_user_id")]
    public long CreatedByUserId { get; set; }

    [Column("result")]
    [StringLength(30)]
    public string Result { get; set; } = null!;

    [Column("description")]
    public string Description { get; set; } = null!;

    [Column("started_at")]
    public DateTime StartedAt { get; set; }

    [Column("completed_at")]
    public DateTime CompletedAt { get; set; }

    [Column("submitted_at")]
    public DateTime? SubmittedAt { get; set; }

    [Column("is_submitted")]
    public bool IsSubmitted { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("CreatedByUserId")]
    [InverseProperty("RepairReports")]
    public virtual AppUser CreatedByUser { get; set; } = null!;

    [ForeignKey("FaultAssignmentId")]
    [InverseProperty("RepairReports")]
    public virtual FaultAssignment FaultAssignment { get; set; } = null!;

    [InverseProperty("RepairReport")]
    public virtual ICollection<RepairReportAction> RepairReportActions { get; set; } = new List<RepairReportAction>();

    [InverseProperty("RepairReport")]
    public virtual ICollection<RepairReportAttachment> RepairReportAttachments { get; set; } = new List<RepairReportAttachment>();

    [InverseProperty("RepairReport")]
    public virtual ICollection<RepairReportPart> RepairReportParts { get; set; } = new List<RepairReportPart>();
}
