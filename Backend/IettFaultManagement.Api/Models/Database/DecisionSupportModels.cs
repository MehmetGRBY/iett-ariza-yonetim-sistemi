using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("root_causes", Schema = "fault_management")]
public class RootCause
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("code"), StringLength(30)] public string Code { get; set; } = null!;
    [Column("name"), StringLength(150)] public string Name { get; set; } = null!;
    [Column("description"), StringLength(1000)] public string? Description { get; set; }
    [Column("is_active")] public bool IsActive { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("solution_articles", Schema = "fault_management")]
public class SolutionArticle
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("fault_category_id")] public long FaultCategoryId { get; set; }
    [Column("root_cause_id")] public long? RootCauseId { get; set; }
    [Column("source_repair_report_id")] public long? SourceRepairReportId { get; set; }
    [Column("title"), StringLength(200)] public string Title { get; set; } = null!;
    [Column("symptoms"), StringLength(1500)] public string Symptoms { get; set; } = null!;
    [Column("solution_steps")] public string SolutionSteps { get; set; } = null!;
    [Column("safety_notes"), StringLength(1500)] public string? SafetyNotes { get; set; }
    [Column("estimated_minutes")] public int? EstimatedMinutes { get; set; }
    [Column("approval_status"), StringLength(20)] public string ApprovalStatus { get; set; } = "DRAFT";
    [Column("created_by_user_id")] public long CreatedByUserId { get; set; }
    [Column("approved_by_user_id")] public long? ApprovedByUserId { get; set; }
    [Column("approved_at")] public DateTime? ApprovedAt { get; set; }
    [Column("is_active")] public bool IsActive { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("vehicle_inspections", Schema = "fault_management")]
public class VehicleInspection
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("vehicle_id")] public long VehicleId { get; set; }
    [Column("fault_id")] public long? FaultId { get; set; }
    [Column("inspection_type"), StringLength(30)] public string InspectionType { get; set; } = null!;
    [Column("result"), StringLength(20)] public string Result { get; set; } = "PENDING";
    [Column("odometer")] public int? Odometer { get; set; }
    [Column("notes"), StringLength(2000)] public string? Notes { get; set; }
    [Column("inspected_by_user_id")] public long? InspectedByUserId { get; set; }
    [Column("inspected_at")] public DateTime? InspectedAt { get; set; }
    [Column("next_action"), StringLength(1000)] public string? NextAction { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("operational_events", Schema = "fault_management")]
public class OperationalEvent
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("event_number"), StringLength(40)] public string EventNumber { get; set; } = null!;
    [Column("event_type"), StringLength(30)] public string EventType { get; set; } = null!;
    [Column("title"), StringLength(200)] public string Title { get; set; } = null!;
    [Column("description"), StringLength(2000)] public string Description { get; set; } = null!;
    [Column("garage_id")] public long? GarageId { get; set; }
    [Column("route_id")] public long? RouteId { get; set; }
    [Column("starts_at")] public DateTime StartsAt { get; set; }
    [Column("ends_at")] public DateTime? EndsAt { get; set; }
    [Column("status"), StringLength(20)] public string Status { get; set; } = "OPEN";
    [Column("created_by_user_id")] public long CreatedByUserId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Keyless, Table("vw_fault_sla_status", Schema = "fault_management")]
public class VwFaultSlaStatus
{
    [Column("fault_id")] public long FaultId { get; set; }
    [Column("fault_number")] public string FaultNumber { get; set; } = null!;
    [Column("garage_id")] public long GarageId { get; set; }
    [Column("vehicle_id")] public long VehicleId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("first_response_at")] public DateTime? FirstResponseAt { get; set; }
    [Column("closed_at")] public DateTime? ClosedAt { get; set; }
    [Column("response_due_at")] public DateTime? ResponseDueAt { get; set; }
    [Column("resolution_due_at")] public DateTime? ResolutionDueAt { get; set; }
    [Column("sla_status")] public string SlaStatus { get; set; } = null!;
}

[Keyless, Table("vw_vehicle_health_scores", Schema = "fault_management")]
public class VwVehicleHealthScore
{
    [Column("vehicle_id")] public long VehicleId { get; set; }
    [Column("door_number")] public string DoorNumber { get; set; } = null!;
    [Column("garage_id")] public long GarageId { get; set; }
    [Column("vehicle_status_id")] public long VehicleStatusId { get; set; }
    [Column("health_score")] public int HealthScore { get; set; }
    [Column("faults_90d")] public long Faults90d { get; set; }
    [Column("faults_30d")] public long Faults30d { get; set; }
    [Column("failed_inspections_90d")] public long FailedInspections90d { get; set; }
}

[Keyless, Table("vw_recurring_vehicle_faults", Schema = "fault_management")]
public class VwRecurringVehicleFault
{
    [Column("vehicle_id")] public long VehicleId { get; set; }
    [Column("fault_category_id")] public long FaultCategoryId { get; set; }
    [Column("fault_count")] public long FaultCount { get; set; }
    [Column("first_fault_at")] public DateTime FirstFaultAt { get; set; }
    [Column("last_fault_at")] public DateTime LastFaultAt { get; set; }
}
