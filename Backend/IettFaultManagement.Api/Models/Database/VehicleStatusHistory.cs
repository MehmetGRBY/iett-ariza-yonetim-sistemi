using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("vehicle_status_histories", Schema = "fault_management")]
public partial class VehicleStatusHistory
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("vehicle_id")]
    public long VehicleId { get; set; }

    [Column("old_status_id")]
    public long? OldStatusId { get; set; }

    [Column("new_status_id")]
    public long NewStatusId { get; set; }

    [Column("changed_by_user_id")]
    public long ChangedByUserId { get; set; }

    [Column("changed_at")]
    public DateTime ChangedAt { get; set; }

    [Column("description")]
    [StringLength(500)]
    public string Description { get; set; } = null!;

    [Column("fault_id")]
    public long? FaultId { get; set; }

    [ForeignKey("ChangedByUserId")]
    [InverseProperty("VehicleStatusHistories")]
    public virtual AppUser ChangedByUser { get; set; } = null!;

    [ForeignKey("FaultId")]
    [InverseProperty("VehicleStatusHistories")]
    public virtual Fault? Fault { get; set; }

    [ForeignKey("NewStatusId")]
    [InverseProperty("VehicleStatusHistoryNewStatuses")]
    public virtual VehicleStatus NewStatus { get; set; } = null!;

    [ForeignKey("OldStatusId")]
    [InverseProperty("VehicleStatusHistoryOldStatuses")]
    public virtual VehicleStatus? OldStatus { get; set; }

    [ForeignKey("VehicleId")]
    [InverseProperty("VehicleStatusHistories")]
    public virtual Vehicle Vehicle { get; set; } = null!;
}
