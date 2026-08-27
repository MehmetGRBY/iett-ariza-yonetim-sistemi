using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("repair_report_parts", Schema = "fault_management")]
public partial class RepairReportPart
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("repair_report_id")]
    public long RepairReportId { get; set; }

    [Column("part_name")]
    [StringLength(200)]
    public string PartName { get; set; } = null!;

    [Column("quantity")]
    [Precision(12, 3)]
    public decimal Quantity { get; set; }

    [Column("description")]
    [StringLength(500)]
    public string? Description { get; set; }

    [ForeignKey("RepairReportId")]
    [InverseProperty("RepairReportParts")]
    public virtual RepairReport RepairReport { get; set; } = null!;
}
