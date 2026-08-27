using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("fault_statuses", Schema = "fault_management")]
[Index("Code", Name = "fault_statuses_code_key", IsUnique = true)]
[Index("Name", Name = "fault_statuses_name_key", IsUnique = true)]
public partial class FaultStatus
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

    [Column("is_closed_status")]
    public bool IsClosedStatus { get; set; }

    [Column("display_order")]
    public int DisplayOrder { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [InverseProperty("NewStatus")]
    public virtual ICollection<FaultStatusHistory> FaultStatusHistoryNewStatuses { get; set; } = new List<FaultStatusHistory>();

    [InverseProperty("OldStatus")]
    public virtual ICollection<FaultStatusHistory> FaultStatusHistoryOldStatuses { get; set; } = new List<FaultStatusHistory>();

    [InverseProperty("FaultStatus")]
    public virtual ICollection<Fault> Faults { get; set; } = new List<Fault>();
}
