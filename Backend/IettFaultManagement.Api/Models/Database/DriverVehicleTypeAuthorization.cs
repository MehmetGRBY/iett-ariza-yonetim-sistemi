using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("driver_vehicle_type_authorizations", Schema = "fault_management")]
[Index("DriverId", "VehicleTypeId", Name = "uq_driver_vehicle_type_authorization", IsUnique = true)]
public partial class DriverVehicleTypeAuthorization
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("driver_id")]
    public long DriverId { get; set; }

    [Column("vehicle_type_id")]
    public long VehicleTypeId { get; set; }

    [Column("authorized_at")]
    public DateTime AuthorizedAt { get; set; }

    [Column("authorized_by_user_id")]
    public long? AuthorizedByUserId { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("deactivated_at")]
    public DateTime? DeactivatedAt { get; set; }

    [Column("description")]
    [StringLength(500)]
    public string? Description { get; set; }

    [ForeignKey("AuthorizedByUserId")]
    [InverseProperty("DriverVehicleTypeAuthorizations")]
    public virtual AppUser? AuthorizedByUser { get; set; }

    [ForeignKey("DriverId")]
    [InverseProperty("DriverVehicleTypeAuthorizations")]
    public virtual Driver Driver { get; set; } = null!;

    [ForeignKey("VehicleTypeId")]
    [InverseProperty("DriverVehicleTypeAuthorizations")]
    public virtual VehicleType VehicleType { get; set; } = null!;
}
