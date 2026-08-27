using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("vehicle_statuses", Schema = "fault_management")]
[Index("Code", Name = "vehicle_statuses_code_key", IsUnique = true)]
[Index("Name", Name = "vehicle_statuses_name_key", IsUnique = true)]
public partial class VehicleStatus
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("code")]
    [StringLength(50)]
    public string Code { get; set; } = null!;

    [Column("name")]
    [StringLength(80)]
    public string Name { get; set; } = null!;

    [Column("display_order")]
    public int DisplayOrder { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [InverseProperty("NewStatus")]
    public virtual ICollection<VehicleStatusHistory> VehicleStatusHistoryNewStatuses { get; set; } = new List<VehicleStatusHistory>();

    [InverseProperty("OldStatus")]
    public virtual ICollection<VehicleStatusHistory> VehicleStatusHistoryOldStatuses { get; set; } = new List<VehicleStatusHistory>();

    [InverseProperty("VehicleStatus")]
    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
