using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("fault_alerts", Schema = "fault_management")]
public partial class FaultAlert
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("fault_id")]
    public long FaultId { get; set; }

    [Column("alert_type")]
    [StringLength(50)]
    public string AlertType { get; set; } = null!;

    [Column("title")]
    [StringLength(200)]
    public string Title { get; set; } = null!;

    [Column("message")]
    [StringLength(1000)]
    public string Message { get; set; } = null!;

    [Column("alert_status")]
    [StringLength(20)]
    public string AlertStatus { get; set; } = null!;

    [Column("triggered_at")]
    public DateTime TriggeredAt { get; set; }

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    [Column("resolved_by_user_id")]
    public long? ResolvedByUserId { get; set; }

    [Column("resolution_note")]
    [StringLength(1000)]
    public string? ResolutionNote { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("FaultId")]
    [InverseProperty("FaultAlerts")]
    public virtual Fault Fault { get; set; } = null!;

    [ForeignKey("ResolvedByUserId")]
    [InverseProperty("FaultAlerts")]
    public virtual AppUser? ResolvedByUser { get; set; }
}
