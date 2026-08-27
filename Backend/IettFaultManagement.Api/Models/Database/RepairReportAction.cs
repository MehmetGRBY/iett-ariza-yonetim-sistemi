using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("repair_report_actions", Schema = "fault_management")]
public partial class RepairReportAction
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("repair_report_id")]
    public long RepairReportId { get; set; }

    [Column("description")]
    public string Description { get; set; } = null!;

    [Column("performed_at")]
    public DateTime PerformedAt { get; set; }

    [ForeignKey("RepairReportId")]
    [InverseProperty("RepairReportActions")]
    public virtual RepairReport RepairReport { get; set; } = null!;
}
