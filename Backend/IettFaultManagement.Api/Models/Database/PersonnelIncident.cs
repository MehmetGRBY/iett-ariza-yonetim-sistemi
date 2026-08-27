using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace IettFaultManagement.Api.Models.Database;
[Table("personnel_incidents", Schema = "fault_management")]
/// <summary>Seferdeki sürücü olayını, rapor süresini, yedek kaynakları ve görev devrini tutar.</summary>
public class PersonnelIncident
{
 [Key, Column("id")] public long Id { get; set; }
 [Column("event_number"), StringLength(40)] public string EventNumber { get; set; } = null!;
 [Column("driver_id")] public long DriverId { get; set; }
 [Column("replacement_driver_id")] public long? ReplacementDriverId { get; set; }
 [Column("vehicle_id")] public long? VehicleId { get; set; }
 [Column("service_vehicle_id")] public long? ServiceVehicleId { get; set; }
 [Column("garage_id")] public long GarageId { get; set; }
 [Column("event_type"), StringLength(30)] public string EventType { get; set; } = null!;
 [Column("status"), StringLength(30)] public string Status { get; set; } = null!;
 [Column("description"), StringLength(1000)] public string Description { get; set; } = null!;
 [Column("occurred_at")] public DateTime OccurredAt { get; set; }
 [Column("absence_start_at")] public DateTime AbsenceStartAt { get; set; }
 [Column("expected_return_at")] public DateTime? ExpectedReturnAt { get; set; }
 [Column("medical_report_number"), StringLength(100)] public string? MedicalReportNumber { get; set; }
 [Column("report_status"), StringLength(20)] public string ReportStatus { get; set; } = "PENDING";
 [Column("report_submitted_at")] public DateTime? ReportSubmittedAt { get; set; }
 [Column("dispatched_at")] public DateTime? DispatchedAt { get; set; }
 [Column("arrival_due_at")] public DateTime? ArrivalDueAt { get; set; }
 [Column("resolved_at")] public DateTime? ResolvedAt { get; set; }
 [Column("transferred_task_count")] public int TransferredTaskCount { get; set; }
 [Column("created_by_user_id")] public long CreatedByUserId { get; set; }
 [Column("created_at")] public DateTime CreatedAt { get; set; }
 [Column("is_active")] public bool IsActive { get; set; }
 [ForeignKey(nameof(DriverId))] public Driver Driver { get; set; } = null!;
 [ForeignKey(nameof(ReplacementDriverId))] public Driver? ReplacementDriver { get; set; }
 [ForeignKey(nameof(VehicleId))] public Vehicle? Vehicle { get; set; }
 [ForeignKey(nameof(ServiceVehicleId))] public Vehicle? ServiceVehicle { get; set; }
 [ForeignKey(nameof(GarageId))] public Garage Garage { get; set; } = null!;
 [ForeignKey(nameof(CreatedByUserId))] public AppUser CreatedByUser { get; set; } = null!;
}
