using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("routes", Schema = "fault_management")]
[Index("Code", Name = "routes_code_key", IsUnique = true)]
public partial class Route
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("code")]
    [StringLength(30)]
    public string Code { get; set; } = null!;

    [Column("name")]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    [Column("start_point")]
    [StringLength(200)]
    public string StartPoint { get; set; } = null!;

    [Column("end_point")]
    [StringLength(200)]
    public string EndPoint { get; set; } = null!;

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("Route")]
    public virtual ICollection<ServiceDuty> ServiceDuties { get; set; } = new List<ServiceDuty>();

    [InverseProperty("Route")]
    public virtual ICollection<ServiceTask> ServiceTasks { get; set; } = new List<ServiceTask>();
}
