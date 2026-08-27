using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("app_users", Schema = "fault_management")]
[Index("PersonnelNumber", Name = "app_users_personnel_number_key", IsUnique = true)]
[Index("GarageId", Name = "ix_app_users_garage_id")]
[Index("RoleId", Name = "ix_app_users_role_id")]
[Index("NormalizedPersonnelNumber", Name = "uq_app_users_normalized_personnel_number", IsUnique = true)]
/// <summary>API hesabını; parola hash'i, rol/garaj kapsamı, kilit bilgisi ve SecurityStamp ile temsil eder.</summary>
public partial class AppUser
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("personnel_number")]
    [StringLength(30)]
    public string PersonnelNumber { get; set; } = null!;

    [Column("first_name")]
    [StringLength(100)]
    public string FirstName { get; set; } = null!;

    [Column("last_name")]
    [StringLength(100)]
    public string LastName { get; set; } = null!;

    // E-posta kanalı yalnızca adresi tanımlanan uygulama kullanıcıları için
    // çalışır; diğer hesaplarda alanın NULL kalması bilinçli bir tercihtir.
    [Column("email")]
    [StringLength(254)]
    [EmailAddress]
    public string? Email { get; set; }

    [Column("password_hash")]
    public string PasswordHash { get; set; } = null!;

    [Column("role_id")]
    public long RoleId { get; set; }

    [Column("garage_id")]
    public long? GarageId { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("deactivated_at")]
    public DateTime? DeactivatedAt { get; set; }

    [Column("deactivated_by_user_id")]
    public long? DeactivatedByUserId { get; set; }

    [Column("deactivation_reason")]
    [StringLength(500)]
    public string? DeactivationReason { get; set; }

    private string _genderCode = "MALE";

    // Personel formlarındaki kısa kodlar PostgreSQL constraint değerleriyle eşitlenir.
    [Column("gender_code")]
    [StringLength(10)]
    public string GenderCode
    {
        get => _genderCode;
        set => _genderCode = value?.Trim().ToUpperInvariant() is "F" or "FEMALE" ? "FEMALE" : "MALE";
    }

    [Column("normalized_personnel_number")]
    [StringLength(30)]
    public string NormalizedPersonnelNumber { get; set; } = null!;

    [Column("must_change_password")]
    public bool MustChangePassword { get; set; }

    [Column("failed_login_count")]
    public int FailedLoginCount { get; set; }

    [Column("locked_until")]
    public DateTime? LockedUntil { get; set; }

    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    [Column("password_changed_at")]
    public DateTime? PasswordChangedAt { get; set; }

    [Column("security_stamp")]
    public Guid SecurityStamp { get; set; }

    [InverseProperty("User")]
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    [ForeignKey("DeactivatedByUserId")]
    [InverseProperty("InverseDeactivatedByUser")]
    public virtual AppUser? DeactivatedByUser { get; set; }

    [InverseProperty("AuthorizedByUser")]
    public virtual ICollection<DriverVehicleTypeAuthorization> DriverVehicleTypeAuthorizations { get; set; } = new List<DriverVehicleTypeAuthorization>();

    [InverseProperty("ResolvedByUser")]
    public virtual ICollection<FaultAlert> FaultAlerts { get; set; } = new List<FaultAlert>();

    [InverseProperty("AssignedByUser")]
    public virtual ICollection<FaultAssignment> FaultAssignments { get; set; } = new List<FaultAssignment>();

    [InverseProperty("UploadedByUser")]
    public virtual ICollection<FaultAttachment> FaultAttachments { get; set; } = new List<FaultAttachment>();

    [InverseProperty("CreatedByUser")]
    public virtual ICollection<Fault> FaultCreatedByUsers { get; set; } = new List<Fault>();

    [InverseProperty("DeactivatedByUser")]
    public virtual ICollection<Fault> FaultDeactivatedByUsers { get; set; } = new List<Fault>();

    [InverseProperty("ChangedByUser")]
    public virtual ICollection<FaultStatusHistory> FaultStatusHistories { get; set; } = new List<FaultStatusHistory>();

    [ForeignKey("GarageId")]
    [InverseProperty("AppUsers")]
    public virtual Garage? Garage { get; set; }

    [InverseProperty("DeactivatedByUser")]
    public virtual ICollection<AppUser> InverseDeactivatedByUser { get; set; } = new List<AppUser>();

    [InverseProperty("User")]
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    [InverseProperty("RequestedByUser")]
    public virtual ICollection<PasswordResetRequest> PasswordResetRequestRequestedByUsers { get; set; } = new List<PasswordResetRequest>();

    [InverseProperty("User")]
    public virtual ICollection<PasswordResetRequest> PasswordResetRequestUsers { get; set; } = new List<PasswordResetRequest>();

    [InverseProperty("UploadedByUser")]
    public virtual ICollection<RepairReportAttachment> RepairReportAttachments { get; set; } = new List<RepairReportAttachment>();

    [InverseProperty("CreatedByUser")]
    public virtual ICollection<RepairReport> RepairReports { get; set; } = new List<RepairReport>();

    [ForeignKey("RoleId")]
    [InverseProperty("AppUsers")]
    public virtual Role Role { get; set; } = null!;

    [InverseProperty("CreatedByUser")]
    public virtual ICollection<ServiceDuty> ServiceDutyCreatedByUsers { get; set; } = new List<ServiceDuty>();

    [InverseProperty("DeactivatedByUser")]
    public virtual ICollection<ServiceDuty> ServiceDutyDeactivatedByUsers { get; set; } = new List<ServiceDuty>();

    [InverseProperty("CreatedByUser")]
    public virtual ICollection<ServiceTask> ServiceTaskCreatedByUsers { get; set; } = new List<ServiceTask>();

    [InverseProperty("DeactivatedByUser")]
    public virtual ICollection<ServiceTask> ServiceTaskDeactivatedByUsers { get; set; } = new List<ServiceTask>();

    [InverseProperty("UpdatedByUser")]
    public virtual ICollection<SystemSetting> SystemSettings { get; set; } = new List<SystemSetting>();

    [InverseProperty("AssignedByUser")]
    public virtual ICollection<TaskAssignment> TaskAssignments { get; set; } = new List<TaskAssignment>();

    [InverseProperty("TransferredByUser")]
    public virtual ICollection<TaskTransferBatch> TaskTransferBatches { get; set; } = new List<TaskTransferBatch>();

    [InverseProperty("User")]
    public virtual TeamMember? TeamMember { get; set; }

    [InverseProperty("CompletedByUser")]
    public virtual ICollection<VehicleDeliveryAssignment> VehicleDeliveryAssignmentCompletedByUsers { get; set; } = new List<VehicleDeliveryAssignment>();

    [InverseProperty("CreatedByUser")]
    public virtual ICollection<VehicleDeliveryAssignment> VehicleDeliveryAssignmentCreatedByUsers { get; set; } = new List<VehicleDeliveryAssignment>();

    [InverseProperty("PerformedByUser")]
    public virtual ICollection<VehicleEventLog> VehicleEventLogs { get; set; } = new List<VehicleEventLog>();

    [InverseProperty("ChangedByUser")]
    public virtual ICollection<VehicleGarageHistory> VehicleGarageHistories { get; set; } = new List<VehicleGarageHistory>();

    [InverseProperty("ChangedByUser")]
    public virtual ICollection<VehicleStatusHistory> VehicleStatusHistories { get; set; } = new List<VehicleStatusHistory>();
}
