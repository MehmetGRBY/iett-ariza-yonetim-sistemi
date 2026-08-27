using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("drivers", Schema = "fault_management")]
[Index("PersonnelNumber", Name = "drivers_personnel_number_key", IsUnique = true)]
/// <summary>Sürücünün sicil, garaj, normal/yedek türü ve anlık müsaitlik durumunu temsil eder.</summary>
public partial class Driver
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("personnel_number")]
    [StringLength(30)]
    public string PersonnelNumber { get; set; } = null!;

    [Column("first_name")]
    [StringLength(100)]
    public string FirstName { get; set; } = null!;

    [Column("last_name")]
    [StringLength(100)]
    public string LastName { get; set; } = null!;

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    private string _genderCode = "MALE";

    // Eski M/F form kodları veritabanının MALE/FEMALE sözlüğüne burada güvenli biçimde çevrilir.
    [Column("gender_code")]
    [StringLength(10)]
    public string GenderCode
    {
        get => _genderCode;
        set => _genderCode = value?.Trim().ToUpperInvariant() is "F" or "FEMALE" ? "FEMALE" : "MALE";
    }

    [Column("garage_id")]
    public long? GarageId { get; set; }

    [Column("driver_type")]
    [StringLength(20)]
    public string DriverType { get; set; } = null!;

    [Column("availability_status")]
    [StringLength(20)]
    public string AvailabilityStatus { get; set; } = null!;

    [InverseProperty("Driver")]
    public virtual ICollection<DriverVehicleTypeAuthorization> DriverVehicleTypeAuthorizations { get; set; } = new List<DriverVehicleTypeAuthorization>();

    [InverseProperty("Driver")]
    public virtual ICollection<Fault> Faults { get; set; } = new List<Fault>();

    [ForeignKey("GarageId")]
    [InverseProperty("Drivers")]
    public virtual Garage? Garage { get; set; }

    [InverseProperty("OriginalDriver")]
    public virtual ICollection<ServiceDuty> ServiceDuties { get; set; } = new List<ServiceDuty>();

    [InverseProperty("Driver")]
    public virtual ICollection<TaskAssignment> TaskAssignments { get; set; } = new List<TaskAssignment>();

    [InverseProperty("Driver")]
    public virtual ICollection<TaskTransferBatch> TaskTransferBatches { get; set; } = new List<TaskTransferBatch>();

    [InverseProperty("DeliveryDriver")]
    public virtual ICollection<VehicleDeliveryAssignment> VehicleDeliveryAssignmentDeliveryDrivers { get; set; } = new List<VehicleDeliveryAssignment>();

    [InverseProperty("ReceivingDriver")]
    public virtual ICollection<VehicleDeliveryAssignment> VehicleDeliveryAssignmentReceivingDrivers { get; set; } = new List<VehicleDeliveryAssignment>();
}
