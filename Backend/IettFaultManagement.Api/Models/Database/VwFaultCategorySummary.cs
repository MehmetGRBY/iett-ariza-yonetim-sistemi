using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Keyless]
public partial class VwFaultCategorySummary
{
    [Column("parent_category_id")]
    public long? ParentCategoryId { get; set; }

    [Column("parent_category_name")]
    [StringLength(120)]
    public string? ParentCategoryName { get; set; }

    [Column("subcategory_id")]
    public long? SubcategoryId { get; set; }

    [Column("subcategory_name", TypeName = "character varying")]
    public string? SubcategoryName { get; set; }

    [Column("total_fault_count")]
    public long? TotalFaultCount { get; set; }

    [Column("open_fault_count")]
    public long? OpenFaultCount { get; set; }

    [Column("closed_fault_count")]
    public long? ClosedFaultCount { get; set; }

    [Column("last_fault_at")]
    public DateTime? LastFaultAt { get; set; }
}
