using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IettFaultManagement.Api.Models.Database;

[Table("fault_resource_status_histories", Schema = "fault_management")]
public class FaultResourceStatusHistory
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("resource_assignment_id")] public long ResourceAssignmentId { get; set; }
    [Column("old_status"), StringLength(30)] public string? OldStatus { get; set; }
    [Column("new_status"), StringLength(30)] public string NewStatus { get; set; } = null!;
    [Column("changed_by_user_id")] public long ChangedByUserId { get; set; }
    [Column("description"), StringLength(1000)] public string Description { get; set; } = null!;
    [Column("changed_at")] public DateTime ChangedAt { get; set; }
}
