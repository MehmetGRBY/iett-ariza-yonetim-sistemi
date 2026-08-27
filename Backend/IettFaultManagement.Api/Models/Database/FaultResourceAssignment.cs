using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IettFaultManagement.Api.Models.Database;

[Table("fault_resource_assignments", Schema = "fault_management")]
public class FaultResourceAssignment
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("fault_id")] public long FaultId { get; set; }
    [Column("resource_type"), StringLength(30)] public string ResourceType { get; set; } = null!;
    [Column("vehicle_id")] public long VehicleId { get; set; }
    [Column("driver_id")] public long? DriverId { get; set; }
    [Column("technician_team_id")] public long? TechnicianTeamId { get; set; }
    [Column("status"), StringLength(30)] public string Status { get; set; } = null!;
    [Column("assigned_at")] public DateTime AssignedAt { get; set; }
    [Column("departed_at")] public DateTime? DepartedAt { get; set; }
    [Column("arrived_at")] public DateTime? ArrivedAt { get; set; }
    [Column("completed_at")] public DateTime? CompletedAt { get; set; }
    [Column("assigned_by_user_id")] public long AssignedByUserId { get; set; }
    [Column("description"), StringLength(1000)] public string Description { get; set; } = null!;
    [Column("is_active")] public bool IsActive { get; set; }
}
