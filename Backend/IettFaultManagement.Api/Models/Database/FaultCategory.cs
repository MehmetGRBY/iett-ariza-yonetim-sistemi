using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("fault_categories", Schema = "fault_management")]
public partial class FaultCategory
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    [StringLength(120)]
    public string Name { get; set; } = null!;

    [Column("parent_category_id")]
    public long? ParentCategoryId { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("estimated_repair_minutes")]
    public int EstimatedRepairMinutes { get; set; }

    [Column("onsite_repair_minutes")]
    public int OnsiteRepairMinutes { get; set; }

    [Column("auto_repair_result")]
    [StringLength(20)]
    public string AutoRepairResult { get; set; } = null!;

    [InverseProperty("FaultCategory")]
    public virtual ICollection<Fault> Faults { get; set; } = new List<Fault>();

    [InverseProperty("ParentCategory")]
    public virtual ICollection<FaultCategory> InverseParentCategory { get; set; } = new List<FaultCategory>();

    [ForeignKey("ParentCategoryId")]
    [InverseProperty("InverseParentCategory")]
    public virtual FaultCategory? ParentCategory { get; set; }
}
