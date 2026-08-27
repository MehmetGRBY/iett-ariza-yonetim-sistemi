using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("fault_status_histories", Schema = "fault_management")]
[Index("FaultId", "ChangedAt", Name = "ix_fault_status_histories_fault_changed", IsDescending = new[] { false, true })]
public partial class FaultStatusHistory
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("fault_id")]
    public long FaultId { get; set; }

    [Column("old_status_id")]
    public long? OldStatusId { get; set; }

    [Column("new_status_id")]
    public long NewStatusId { get; set; }

    [Column("changed_by_user_id")]
    public long ChangedByUserId { get; set; }

    [Column("changed_by_role_id")]
    public long ChangedByRoleId { get; set; }

    [Column("description")]
    [StringLength(1000)]
    public string Description { get; set; } = null!;

    [Column("is_system_action")]
    public bool IsSystemAction { get; set; }

    [Column("changed_at")]
    public DateTime ChangedAt { get; set; }

    [ForeignKey("ChangedByRoleId")]
    [InverseProperty("FaultStatusHistories")]
    public virtual Role ChangedByRole { get; set; } = null!;

    [ForeignKey("ChangedByUserId")]
    [InverseProperty("FaultStatusHistories")]
    public virtual AppUser ChangedByUser { get; set; } = null!;

    [ForeignKey("FaultId")]
    [InverseProperty("FaultStatusHistories")]
    public virtual Fault Fault { get; set; } = null!;

    [ForeignKey("NewStatusId")]
    [InverseProperty("FaultStatusHistoryNewStatuses")]
    public virtual FaultStatus NewStatus { get; set; } = null!;

    [ForeignKey("OldStatusId")]
    [InverseProperty("FaultStatusHistoryOldStatuses")]
    public virtual FaultStatus? OldStatus { get; set; }
}
