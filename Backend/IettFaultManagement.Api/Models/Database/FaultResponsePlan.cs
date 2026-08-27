using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IettFaultManagement.Api.Models.Database;

[Table("fault_response_plans", Schema = "fault_management")]
public class FaultResponsePlan
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("fault_id")] public long FaultId { get; set; }
    [Column("mobility_status"), StringLength(30)] public string MobilityStatus { get; set; } = null!;
    [Column("can_complete_current_trip")] public bool CanCompleteCurrentTrip { get; set; }
    [Column("can_continue_remaining_tasks")] public bool CanContinueRemainingTasks { get; set; }
    [Column("on_site_repair_possible")] public bool? OnSiteRepairPossible { get; set; }
    [Column("tow_required")] public bool TowRequired { get; set; }
    [Column("service_vehicle_required")] public bool ServiceVehicleRequired { get; set; }
    [Column("replacement_vehicle_required")] public bool ReplacementVehicleRequired { get; set; }
    [Column("driver_can_continue")] public bool DriverCanContinue { get; set; }
    [Column("assessment_note"), StringLength(1000)] public string AssessmentNote { get; set; } = null!;
    [Column("assessed_by_user_id")] public long AssessedByUserId { get; set; }
    [Column("assessed_at")] public DateTime AssessedAt { get; set; }
    [Column("is_active")] public bool IsActive { get; set; }
    [Column("automation_enabled")] public bool AutomationEnabled { get; set; }
    [Column("automation_status"), StringLength(30)] public string AutomationStatus { get; set; } = null!;
    [Column("next_automation_at")] public DateTime? NextAutomationAt { get; set; }
    [Column("planned_repair_minutes")] public int PlannedRepairMinutes { get; set; }
    [Column("planned_repair_result"), StringLength(20)] public string PlannedRepairResult { get; set; } = null!;
    [Column("repair_started_at")] public DateTime? RepairStartedAt { get; set; }
    [Column("automation_completed_at")] public DateTime? AutomationCompletedAt { get; set; }
    [Column("last_automation_error")] public string? LastAutomationError { get; set; }
    // Eski veritabanı uyumluluğu için tutulur; uygulama tek yarı otomatik akış kullanır.
    [Column("operation_mode"), StringLength(20)] public string OperationMode { get; set; } = "MANUAL";
    [Column("inspection_attempt_count")] public int InspectionAttemptCount { get; set; }
    [Column("max_inspection_attempts")] public int MaxInspectionAttempts { get; set; } = 3;
    [Column("ready_to_close")] public bool ReadyToClose { get; set; }
}
