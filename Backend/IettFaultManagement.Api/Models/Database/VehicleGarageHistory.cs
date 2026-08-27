using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("vehicle_garage_histories", Schema = "fault_management")]
public partial class VehicleGarageHistory
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("vehicle_id")]
    public long VehicleId { get; set; }

    [Column("old_garage_id")]
    public long? OldGarageId { get; set; }

    [Column("new_garage_id")]
    public long NewGarageId { get; set; }

    [Column("changed_by_user_id")]
    public long ChangedByUserId { get; set; }

    [Column("changed_at")]
    public DateTime ChangedAt { get; set; }

    [Column("description")]
    [StringLength(500)]
    public string Description { get; set; } = null!;

    [ForeignKey("ChangedByUserId")]
    [InverseProperty("VehicleGarageHistories")]
    public virtual AppUser ChangedByUser { get; set; } = null!;

    [ForeignKey("NewGarageId")]
    [InverseProperty("VehicleGarageHistoryNewGarages")]
    public virtual Garage NewGarage { get; set; } = null!;

    [ForeignKey("OldGarageId")]
    [InverseProperty("VehicleGarageHistoryOldGarages")]
    public virtual Garage? OldGarage { get; set; }

    [ForeignKey("VehicleId")]
    [InverseProperty("VehicleGarageHistories")]
    public virtual Vehicle Vehicle { get; set; } = null!;
}
