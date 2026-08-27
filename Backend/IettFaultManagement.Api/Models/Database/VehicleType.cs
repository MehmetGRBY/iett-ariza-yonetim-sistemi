using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("vehicle_types", Schema = "fault_management")]
[Index("Name", Name = "vehicle_types_name_key", IsUnique = true)]
public partial class VehicleType
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    [StringLength(80)]
    public string Name { get; set; } = null!;

    [Column("is_active")]
    public bool IsActive { get; set; }

    [InverseProperty("VehicleType")]
    public virtual ICollection<DriverVehicleTypeAuthorization> DriverVehicleTypeAuthorizations { get; set; } = new List<DriverVehicleTypeAuthorization>();

    [InverseProperty("VehicleType")]
    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
