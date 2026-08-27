using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("garages", Schema = "fault_management")]
[Index("Code", Name = "garages_code_key", IsUnique = true)]
[Index("Name", Name = "garages_name_key", IsUnique = true)]
/// <summary>Garajın kodu, kapasitesi, aktifliği ve kendisine bağlı araç/personel/ekip ilişkilerini temsil eder.</summary>
public partial class Garage
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("code")]
    [StringLength(30)]
    public string Code { get; set; } = null!;

    [Column("name")]
    [StringLength(150)]
    public string Name { get; set; } = null!;

    [Column("address")]
    [StringLength(500)]
    public string? Address { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("vehicle_capacity")]
    public int VehicleCapacity { get; set; }

    [InverseProperty("Garage")]
    public virtual ICollection<AppUser> AppUsers { get; set; } = new List<AppUser>();

    [InverseProperty("Garage")]
    public virtual ICollection<Driver> Drivers { get; set; } = new List<Driver>();

    [InverseProperty("Garage")]
    public virtual ICollection<Fault> Faults { get; set; } = new List<Fault>();

    [InverseProperty("Garage")]
    public virtual ICollection<ServiceDuty> ServiceDuties { get; set; } = new List<ServiceDuty>();

    [InverseProperty("Garage")]
    public virtual ICollection<TaskTransferBatch> TaskTransferBatches { get; set; } = new List<TaskTransferBatch>();

    [InverseProperty("Garage")]
    public virtual ICollection<TechnicianTeam> TechnicianTeams { get; set; } = new List<TechnicianTeam>();

    [InverseProperty("Garage")]
    public virtual ICollection<VehicleDeliveryAssignment> VehicleDeliveryAssignments { get; set; } = new List<VehicleDeliveryAssignment>();

    [InverseProperty("NewGarage")]
    public virtual ICollection<VehicleGarageHistory> VehicleGarageHistoryNewGarages { get; set; } = new List<VehicleGarageHistory>();

    [InverseProperty("OldGarage")]
    public virtual ICollection<VehicleGarageHistory> VehicleGarageHistoryOldGarages { get; set; } = new List<VehicleGarageHistory>();

    [InverseProperty("Garage")]
    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
