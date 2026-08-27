using System;
using System.Collections.Generic;
using IettFaultManagement.Api.Models.Database;
using Microsoft.EntityFrameworkCore;
using DatabaseRoute = IettFaultManagement.Api.Models.Database.Route;

namespace IettFaultManagement.Api.Data;

/// <summary>
/// PostgreSQL fault_management şeması ile EF Core arasındaki ana köprüdür.
/// DbSet'ler tablo/view'ları, OnModelCreating ise PK, FK, index, constraint ve ilişkileri eşler.
/// Bu dosya Database First scaffold ile üretildiğinden şema değişince yeniden oluşturulabilir.
/// </summary>
public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AppUser> AppUsers { get; set; }
    public virtual DbSet<AiSuggestion> AiSuggestions { get; set; }
    public virtual DbSet<AiSuggestionSource> AiSuggestionSources { get; set; }
    public virtual DbSet<AiFeedback> AiFeedback { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Driver> Drivers { get; set; }

    public virtual DbSet<DriverVehicleTypeAuthorization> DriverVehicleTypeAuthorizations { get; set; }

    public virtual DbSet<EmailOutbox> EmailOutbox { get; set; }

    public virtual DbSet<Fault> Faults { get; set; }

    public virtual DbSet<FaultResponsePlan> FaultResponsePlans { get; set; }

    public virtual DbSet<FaultResourceAssignment> FaultResourceAssignments { get; set; }

    public virtual DbSet<FaultResourceStatusHistory> FaultResourceStatusHistories { get; set; }

    public virtual DbSet<FaultAlert> FaultAlerts { get; set; }

    public virtual DbSet<FaultAssignment> FaultAssignments { get; set; }

    public virtual DbSet<FaultAttachment> FaultAttachments { get; set; }

    public virtual DbSet<FaultCategory> FaultCategories { get; set; }

    public virtual DbSet<FaultStatus> FaultStatuses { get; set; }

    public virtual DbSet<FaultStatusHistory> FaultStatusHistories { get; set; }

    public virtual DbSet<FuelType> FuelTypes { get; set; }

    public virtual DbSet<Garage> Garages { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<PersonnelIncident> PersonnelIncidents { get; set; }

    public virtual DbSet<PasswordResetRequest> PasswordResetRequests { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<RepairReport> RepairReports { get; set; }

    public virtual DbSet<RepairReportAction> RepairReportActions { get; set; }

    public virtual DbSet<RepairReportAttachment> RepairReportAttachments { get; set; }

    public virtual DbSet<RepairReportPart> RepairReportParts { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<DatabaseRoute> Routes { get; set; }

    public virtual DbSet<ServiceDuty> ServiceDuties { get; set; }

    public virtual DbSet<ServiceTask> ServiceTasks { get; set; }

    public virtual DbSet<SystemSetting> SystemSettings { get; set; }

    public virtual DbSet<TaskAssignment> TaskAssignments { get; set; }

    public virtual DbSet<TaskTransferBatch> TaskTransferBatches { get; set; }

    public virtual DbSet<TeamMember> TeamMembers { get; set; }

    public virtual DbSet<TechnicianTeam> TechnicianTeams { get; set; }

    public virtual DbSet<Vehicle> Vehicles { get; set; }

    public virtual DbSet<VehicleDeliveryAssignment> VehicleDeliveryAssignments { get; set; }

    public virtual DbSet<VehicleEventLog> VehicleEventLogs { get; set; }

    public virtual DbSet<VehicleGarageHistory> VehicleGarageHistories { get; set; }

    public virtual DbSet<VehicleStatus> VehicleStatuses { get; set; }

    public virtual DbSet<VehicleStatusHistory> VehicleStatusHistories { get; set; }

    public virtual DbSet<VehicleType> VehicleTypes { get; set; }

    public virtual DbSet<VwActiveFault> VwActiveFaults { get; set; }

    public virtual DbSet<VwActiveVehicleDelivery> VwActiveVehicleDeliveries { get; set; }

    public virtual DbSet<VwAvailableDriver> VwAvailableDrivers { get; set; }

    public virtual DbSet<VwAvailableTechnicianTeam> VwAvailableTechnicianTeams { get; set; }

    public virtual DbSet<VwAvailableVehicle> VwAvailableVehicles { get; set; }

    public virtual DbSet<VwDailyFaultSummary> VwDailyFaultSummaries { get; set; }

    public virtual DbSet<VwDriverFaultSummary> VwDriverFaultSummaries { get; set; }

    public virtual DbSet<VwFaultCategorySummary> VwFaultCategorySummaries { get; set; }

    public virtual DbSet<VwFaultRepairDetail> VwFaultRepairDetails { get; set; }

    public virtual DbSet<VwFaultResolutionTime> VwFaultResolutionTimes { get; set; }

    public virtual DbSet<VwGarageOccupancy> VwGarageOccupancies { get; set; }

    public virtual DbSet<VwGarageVehicleTypeSummary> VwGarageVehicleTypeSummaries { get; set; }

    public virtual DbSet<VwPendingPasswordReset> VwPendingPasswordResets { get; set; }

    public virtual DbSet<VwServiceDutySummary> VwServiceDutySummaries { get; set; }

    public virtual DbSet<VwTaskTransferSummary> VwTaskTransferSummaries { get; set; }

    public virtual DbSet<VwTasksWaitingForTransfer> VwTasksWaitingForTransfers { get; set; }

    public virtual DbSet<VwTeamWorkload> VwTeamWorkloads { get; set; }

    public virtual DbSet<VwUnreadNotificationCount> VwUnreadNotificationCounts { get; set; }

    public virtual DbSet<VwVehicleCurrentTask> VwVehicleCurrentTasks { get; set; }

    public virtual DbSet<VwVehicleDeliveryHistory> VwVehicleDeliveryHistories { get; set; }

    public virtual DbSet<VwVehicleFaultSummary> VwVehicleFaultSummaries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("app_users_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.FailedLoginCount).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MustChangePassword).HasDefaultValue(true);
            entity.Property(e => e.SecurityStamp).HasDefaultValueSql("gen_random_uuid()");

            entity.HasOne(d => d.DeactivatedByUser).WithMany(p => p.InverseDeactivatedByUser)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("app_users_deactivated_by_user_id_fkey");

            entity.HasOne(d => d.Garage).WithMany(p => p.AppUsers)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("app_users_garage_id_fkey");

            entity.HasOne(d => d.Role).WithMany(p => p.AppUsers)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("app_users_role_id_fkey");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("audit_logs_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Role).WithMany(p => p.AuditLogs)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("audit_logs_role_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("audit_logs_user_id_fkey");
        });

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("drivers_pkey");

            entity.HasIndex(e => new { e.GarageId, e.DriverType, e.AvailabilityStatus }, "ix_drivers_garage_type_status").HasFilter("is_active");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.AvailabilityStatus).HasDefaultValueSql("'AVAILABLE'::character varying");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.DriverType).HasDefaultValueSql("'NORMAL'::character varying");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Garage).WithMany(p => p.Drivers)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("drivers_garage_id_fkey");
        });

        modelBuilder.Entity<DriverVehicleTypeAuthorization>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("driver_vehicle_type_authorizations_pkey");

            entity.HasIndex(e => new { e.VehicleTypeId, e.DriverId }, "ix_driver_vehicle_authorizations_type_active").HasFilter("is_active");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.AuthorizedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.AuthorizedByUser).WithMany(p => p.DriverVehicleTypeAuthorizations)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("driver_vehicle_type_authorizations_authorized_by_user_id_fkey");

            entity.HasOne(d => d.Driver).WithMany(p => p.DriverVehicleTypeAuthorizations)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("driver_vehicle_type_authorizations_driver_id_fkey");

            entity.HasOne(d => d.VehicleType).WithMany(p => p.DriverVehicleTypeAuthorizations)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("driver_vehicle_type_authorizations_vehicle_type_id_fkey");
        });

        modelBuilder.Entity<Fault>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("faults_pkey");

            entity.HasIndex(e => new { e.GarageId, e.FaultStatusId, e.CreatedAt }, "ix_faults_garage_status_created")
                .IsDescending(false, false, true)
                .HasFilter("(is_active = true)");

            entity.HasIndex(e => e.ServiceTaskId, "ix_faults_service_task_id").HasFilter("(service_task_id IS NOT NULL)");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.FaultCreatedByUsers)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("faults_created_by_user_id_fkey");

            entity.HasOne(d => d.DeactivatedByUser).WithMany(p => p.FaultDeactivatedByUsers)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("faults_deactivated_by_user_id_fkey");

            entity.HasOne(d => d.Driver).WithMany(p => p.Faults)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("faults_driver_id_fkey");

            entity.HasOne(d => d.FaultCategory).WithMany(p => p.Faults)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("faults_fault_category_id_fkey");

            entity.HasOne(d => d.FaultStatus).WithMany(p => p.Faults)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("faults_fault_status_id_fkey");

            entity.HasOne(d => d.Garage).WithMany(p => p.Faults)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("faults_garage_id_fkey");

            entity.HasOne(d => d.ServiceTask).WithMany(p => p.Faults)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("faults_service_task_id_fkey");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.Faults)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("faults_vehicle_id_fkey");
        });

        modelBuilder.Entity<FaultAlert>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("fault_alerts_pkey");

            entity.HasIndex(e => new { e.FaultId, e.TriggeredAt }, "ix_fault_alerts_open")
                .IsDescending(false, true)
                .HasFilter("((alert_status)::text <> 'RESOLVED'::text)");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.AlertStatus).HasDefaultValueSql("'OPEN'::character varying");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.TriggeredAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Fault).WithMany(p => p.FaultAlerts)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fault_alerts_fault_id_fkey");

            entity.HasOne(d => d.ResolvedByUser).WithMany(p => p.FaultAlerts)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fault_alerts_resolved_by_user_id_fkey");
        });

        modelBuilder.Entity<FaultAssignment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("fault_assignments_pkey");

            entity.HasIndex(e => e.TeamId, "ix_fault_assignments_team_active").HasFilter("(is_active = true)");

            entity.HasIndex(e => e.FaultId, "uq_fault_assignments_active_fault")
                .IsUnique()
                .HasFilter("(is_active = true)");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.AssignedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsAutomatic).HasDefaultValue(true);

            entity.HasOne(d => d.AssignedByUser).WithMany(p => p.FaultAssignments)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fault_assignments_assigned_by_user_id_fkey");

            entity.HasOne(d => d.Fault).WithMany(p => p.FaultAssignments)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fault_assignments_fault_id_fkey");

            entity.HasOne(d => d.Team).WithMany(p => p.FaultAssignments)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fault_assignments_team_id_fkey");
        });

        modelBuilder.Entity<FaultAttachment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("fault_attachments_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Fault).WithMany(p => p.FaultAttachments)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fault_attachments_fault_id_fkey");

            entity.HasOne(d => d.UploadedByUser).WithMany(p => p.FaultAttachments)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fault_attachments_uploaded_by_user_id_fkey");
        });

        modelBuilder.Entity<FaultCategory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("fault_categories_pkey");

            entity.HasIndex(e => new { e.ParentCategoryId, e.Name }, "uq_fault_categories_child_name")
                .IsUnique()
                .HasFilter("(parent_category_id IS NOT NULL)");

            entity.HasIndex(e => e.Name, "uq_fault_categories_root_name")
                .IsUnique()
                .HasFilter("(parent_category_id IS NULL)");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.ParentCategory).WithMany(p => p.InverseParentCategory)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fault_categories_parent_category_id_fkey");
        });

        modelBuilder.Entity<FaultStatus>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("fault_statuses_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsClosedStatus).HasDefaultValue(false);
        });

        modelBuilder.Entity<FaultStatusHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("fault_status_histories_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.ChangedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsSystemAction).HasDefaultValue(false);

            entity.HasOne(d => d.ChangedByRole).WithMany(p => p.FaultStatusHistories)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fault_status_histories_changed_by_role_id_fkey");

            entity.HasOne(d => d.ChangedByUser).WithMany(p => p.FaultStatusHistories)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fault_status_histories_changed_by_user_id_fkey");

            entity.HasOne(d => d.Fault).WithMany(p => p.FaultStatusHistories)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fault_status_histories_fault_id_fkey");

            entity.HasOne(d => d.NewStatus).WithMany(p => p.FaultStatusHistoryNewStatuses)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fault_status_histories_new_status_id_fkey");

            entity.HasOne(d => d.OldStatus).WithMany(p => p.FaultStatusHistoryOldStatuses)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fault_status_histories_old_status_id_fkey");
        });

        modelBuilder.Entity<FuelType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("fuel_types_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<Garage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("garages_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.VehicleCapacity).HasDefaultValue(0);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("notifications_pkey");

            entity.HasIndex(e => e.ServiceTaskId, "ix_notifications_service_task").HasFilter("(service_task_id IS NOT NULL)");

            entity.HasIndex(e => e.TaskTransferBatchId, "ix_notifications_transfer_batch").HasFilter("(task_transfer_batch_id IS NOT NULL)");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "ix_notifications_user_unread")
                .IsDescending(false, true)
                .HasFilter("(is_read = false)");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.NotificationType).HasDefaultValueSql("'SYSTEM'::character varying");

            entity.HasOne(d => d.Fault).WithMany(p => p.Notifications)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("notifications_fault_id_fkey");

            entity.HasOne(d => d.ServiceTask).WithMany(p => p.Notifications)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("notifications_service_task_id_fkey");

            entity.HasOne(d => d.TaskTransferBatch).WithMany(p => p.Notifications)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("notifications_task_transfer_batch_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("notifications_user_id_fkey");
        });

        modelBuilder.Entity<PasswordResetRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("password_reset_requests_pkey");

            entity.HasIndex(e => e.ExpiresAt, "ix_password_reset_open_expiry").HasFilter("((used_at IS NULL) AND (revoked_at IS NULL))");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.RequestType).HasDefaultValueSql("'SELF_SERVICE'::character varying");
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.RequestedByUser).WithMany(p => p.PasswordResetRequestRequestedByUsers)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("password_reset_requests_requested_by_user_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.PasswordResetRequestUsers)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("password_reset_requests_user_id_fkey");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("permissions_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
        });

        modelBuilder.Entity<RepairReport>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("repair_reports_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsSubmitted).HasDefaultValue(false);

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.RepairReports)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("repair_reports_created_by_user_id_fkey");

            entity.HasOne(d => d.FaultAssignment).WithMany(p => p.RepairReports)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("repair_reports_fault_assignment_id_fkey");
        });

        modelBuilder.Entity<RepairReportAction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("repair_report_actions_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.PerformedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.RepairReport).WithMany(p => p.RepairReportActions)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("repair_report_actions_repair_report_id_fkey");
        });

        modelBuilder.Entity<RepairReportAttachment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("repair_report_attachments_pkey");

            entity.HasIndex(e => new { e.RepairReportId, e.UploadedAt }, "ix_repair_report_attachments_report")
                .IsDescending(false, true)
                .HasFilter("(is_active = true)");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.RepairReport).WithMany(p => p.RepairReportAttachments)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("repair_report_attachments_repair_report_id_fkey");

            entity.HasOne(d => d.UploadedByUser).WithMany(p => p.RepairReportAttachments)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("repair_report_attachments_uploaded_by_user_id_fkey");
        });

        modelBuilder.Entity<RepairReportPart>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("repair_report_parts_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();

            entity.HasOne(d => d.RepairReport).WithMany(p => p.RepairReportParts)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("repair_report_parts_repair_report_id_fkey");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("roles_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasMany(d => d.Permissions).WithMany(p => p.Roles)
                .UsingEntity<Dictionary<string, object>>(
                    "RolePermission",
                    r => r.HasOne<Permission>().WithMany()
                        .HasForeignKey("PermissionId")
                        .HasConstraintName("role_permissions_permission_id_fkey"),
                    l => l.HasOne<Role>().WithMany()
                        .HasForeignKey("RoleId")
                        .HasConstraintName("role_permissions_role_id_fkey"),
                    j =>
                    {
                        j.HasKey("RoleId", "PermissionId").HasName("role_permissions_pkey");
                        j.ToTable("role_permissions", "fault_management");
                        j.IndexerProperty<long>("RoleId").HasColumnName("role_id");
                        j.IndexerProperty<long>("PermissionId").HasColumnName("permission_id");
                    });
        });

        modelBuilder.Entity<DatabaseRoute>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("routes_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<ServiceDuty>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("service_duties_pkey");

            entity.HasIndex(e => new { e.ServiceDate, e.GarageId, e.Status }, "ix_service_duties_date_garage_status").HasFilter("is_active");

            entity.HasIndex(e => new { e.OriginalDriverId, e.ServiceDate }, "ix_service_duties_driver_date").HasFilter("(is_active AND (original_driver_id IS NOT NULL))");

            entity.HasIndex(e => new { e.OriginalVehicleId, e.ServiceDate }, "ix_service_duties_vehicle_date").HasFilter("(is_active AND (original_vehicle_id IS NOT NULL))");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Status).HasDefaultValueSql("'PLANNED'::character varying");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.ServiceDutyCreatedByUsers)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("service_duties_created_by_user_id_fkey");

            entity.HasOne(d => d.DeactivatedByUser).WithMany(p => p.ServiceDutyDeactivatedByUsers)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("service_duties_deactivated_by_user_id_fkey");

            entity.HasOne(d => d.Garage).WithMany(p => p.ServiceDuties)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("service_duties_garage_id_fkey");

            entity.HasOne(d => d.OriginalDriver).WithMany(p => p.ServiceDuties)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("service_duties_original_driver_id_fkey");

            entity.HasOne(d => d.OriginalVehicle).WithMany(p => p.ServiceDuties)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("service_duties_original_vehicle_id_fkey");

            entity.HasOne(d => d.Route).WithMany(p => p.ServiceDuties)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("service_duties_route_id_fkey");
        });

        modelBuilder.Entity<ServiceTask>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("service_tasks_pkey");

            entity.HasIndex(e => new { e.ServiceDate, e.Status }, "ix_service_tasks_date_status").HasFilter("(is_active = true)");

            entity.HasIndex(e => new { e.Status, e.ServiceDate }, "ix_service_tasks_status_date").HasFilter("(is_active = true)");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Status).HasDefaultValueSql("'PLANNED'::character varying");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.ServiceTaskCreatedByUsers)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("service_tasks_created_by_user_id_fkey");

            entity.HasOne(d => d.DeactivatedByUser).WithMany(p => p.ServiceTaskDeactivatedByUsers)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("service_tasks_deactivated_by_user_id_fkey");

            entity.HasOne(d => d.Route).WithMany(p => p.ServiceTasks)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("service_tasks_route_id_fkey");

            entity.HasOne(d => d.ServiceDuty).WithMany(p => p.ServiceTasks)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("service_tasks_service_duty_id_fkey");
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("system_settings_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.UpdatedByUser).WithMany(p => p.SystemSettings)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("system_settings_updated_by_user_id_fkey");
        });

        modelBuilder.Entity<TaskAssignment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("task_assignments_pkey");

            entity.HasIndex(e => e.DriverId, "ix_task_assignments_driver_active").HasFilter("(is_active = true)");

            entity.HasIndex(e => e.VehicleId, "ix_task_assignments_vehicle_active").HasFilter("(is_active = true)");

            entity.HasIndex(e => e.ServiceTaskId, "uq_task_assignments_active_task")
                .IsUnique()
                .HasFilter("(is_active = true)");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.AssignedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.AssignmentType).HasDefaultValueSql("'ORIGINAL'::character varying");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.AssignedByUser).WithMany(p => p.TaskAssignments)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("task_assignments_assigned_by_user_id_fkey");

            entity.HasOne(d => d.Driver).WithMany(p => p.TaskAssignments)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("task_assignments_driver_id_fkey");

            entity.HasOne(d => d.ServiceTask).WithMany(p => p.TaskAssignments)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("task_assignments_service_task_id_fkey");

            entity.HasOne(d => d.TransferBatch).WithMany(p => p.TaskAssignments)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("task_assignments_transfer_batch_id_fkey");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.TaskAssignments)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("task_assignments_vehicle_id_fkey");
        });

        modelBuilder.Entity<TaskTransferBatch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("task_transfer_batches_pkey");

            entity.HasIndex(e => new { e.ServiceDutyId, e.TransferredAt }, "ix_task_transfer_batches_duty")
                .IsDescending(false, true)
                .HasFilter("(service_duty_id IS NOT NULL)");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.DriverCanContinue).HasDefaultValue(true);
            entity.Property(e => e.IsAutomatic).HasDefaultValue(true);
            entity.Property(e => e.TransferredAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Driver).WithMany(p => p.TaskTransferBatches)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("task_transfer_batches_driver_id_fkey");

            entity.HasOne(d => d.Fault).WithMany(p => p.TaskTransferBatches)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("task_transfer_batches_fault_id_fkey");

            entity.HasOne(d => d.Garage).WithMany(p => p.TaskTransferBatches)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("task_transfer_batches_garage_id_fkey");

            entity.HasOne(d => d.NewVehicle).WithMany(p => p.TaskTransferBatchNewVehicles)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("task_transfer_batches_new_vehicle_id_fkey");

            entity.HasOne(d => d.OldVehicle).WithMany(p => p.TaskTransferBatchOldVehicles)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("task_transfer_batches_old_vehicle_id_fkey");

            entity.HasOne(d => d.ServiceDuty).WithMany(p => p.TaskTransferBatches)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("task_transfer_batches_service_duty_id_fkey");

            entity.HasOne(d => d.TransferredByUser).WithMany(p => p.TaskTransferBatches)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("task_transfer_batches_transferred_by_user_id_fkey");
        });

        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("team_members_pkey");

            entity.HasIndex(e => e.UserId, "uq_team_members_active_user")
                .IsUnique()
                .HasFilter("(is_active = true)");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsTeamLeader).HasDefaultValue(false);
            entity.Property(e => e.JoinedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Team).WithMany(p => p.TeamMembers)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("team_members_team_id_fkey");

            entity.HasOne(d => d.User).WithOne(p => p.TeamMember)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("team_members_user_id_fkey");
        });

        modelBuilder.Entity<TechnicianTeam>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("technician_teams_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsAvailable).HasDefaultValue(true);

            entity.HasOne(d => d.Garage).WithMany(p => p.TechnicianTeams)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("technician_teams_garage_id_fkey");
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("vehicles_pkey");

            entity.HasIndex(e => new { e.GarageId, e.VehicleStatusId }, "ix_vehicles_garage_status").HasFilter("(is_active = true)");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CurrentMileage).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.FuelType).WithMany(p => p.Vehicles)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicles_fuel_type_id_fkey");

            entity.HasOne(d => d.Garage).WithMany(p => p.Vehicles)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicles_garage_id_fkey");

            entity.HasOne(d => d.VehicleStatus).WithMany(p => p.Vehicles)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicles_vehicle_status_id_fkey");

            entity.HasOne(d => d.VehicleType).WithMany(p => p.Vehicles)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicles_vehicle_type_id_fkey");
        });

        modelBuilder.Entity<VehicleDeliveryAssignment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("vehicle_delivery_assignments_pkey");

            entity.HasIndex(e => new { e.DeliveryDriverId, e.DeliveryStatus }, "ix_vehicle_delivery_driver_status").HasFilter("is_active");

            entity.HasIndex(e => new { e.GarageId, e.DeliveryStatus, e.PlannedAt }, "ix_vehicle_delivery_garage_status")
                .IsDescending(false, false, true)
                .HasFilter("is_active");

            entity.HasIndex(e => e.TransferBatchId, "ix_vehicle_delivery_transfer_batch").HasFilter("(transfer_batch_id IS NOT NULL)");

            entity.HasIndex(e => e.FaultId, "uq_vehicle_delivery_active_fault")
                .IsUnique()
                .HasFilter("(is_active AND ((delivery_status)::text = ANY ((ARRAY['PLANNED'::character varying, 'IN_PROGRESS'::character varying, 'ARRIVED'::character varying])::text[])))");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.DeliveryStatus).HasDefaultValueSql("'PLANNED'::character varying");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsAutomatic).HasDefaultValue(true);
            entity.Property(e => e.PlannedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.BrokenVehicle).WithMany(p => p.VehicleDeliveryAssignmentBrokenVehicles)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_delivery_assignments_broken_vehicle_id_fkey");

            entity.HasOne(d => d.CompletedByUser).WithMany(p => p.VehicleDeliveryAssignmentCompletedByUsers)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_delivery_assignments_completed_by_user_id_fkey");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.VehicleDeliveryAssignmentCreatedByUsers)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_delivery_assignments_created_by_user_id_fkey");

            entity.HasOne(d => d.DeliveryDriver).WithMany(p => p.VehicleDeliveryAssignmentDeliveryDrivers)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_delivery_assignments_delivery_driver_id_fkey");

            entity.HasOne(d => d.Fault).WithOne(p => p.VehicleDeliveryAssignment)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_delivery_assignments_fault_id_fkey");

            entity.HasOne(d => d.Garage).WithMany(p => p.VehicleDeliveryAssignments)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_delivery_assignments_garage_id_fkey");

            entity.HasOne(d => d.ReceivingDriver).WithMany(p => p.VehicleDeliveryAssignmentReceivingDrivers)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_delivery_assignments_receiving_driver_id_fkey");

            entity.HasOne(d => d.ReplacementVehicle).WithMany(p => p.VehicleDeliveryAssignmentReplacementVehicles)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_delivery_assignments_replacement_vehicle_id_fkey");

            entity.HasOne(d => d.SupportVehicle).WithMany(p => p.VehicleDeliveryAssignmentSupportVehicles)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_delivery_assignments_support_vehicle_id_fkey");

            entity.HasOne(d => d.TransferBatch).WithMany(p => p.VehicleDeliveryAssignments)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_delivery_assignments_transfer_batch_id_fkey");
        });

        modelBuilder.Entity<VehicleEventLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("vehicle_event_logs_pkey");

            entity.HasIndex(e => e.FaultId, "ix_vehicle_event_logs_fault").HasFilter("(fault_id IS NOT NULL)");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsSystemAction).HasDefaultValue(false);
            entity.Property(e => e.OccurredAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Fault).WithMany(p => p.VehicleEventLogs)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_event_logs_fault_id_fkey");

            entity.HasOne(d => d.PerformedByUser).WithMany(p => p.VehicleEventLogs)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_event_logs_performed_by_user_id_fkey");

            entity.HasOne(d => d.ServiceTask).WithMany(p => p.VehicleEventLogs)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_event_logs_service_task_id_fkey");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.VehicleEventLogs)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_event_logs_vehicle_id_fkey");
        });

        modelBuilder.Entity<VehicleGarageHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("vehicle_garage_histories_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.ChangedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.ChangedByUser).WithMany(p => p.VehicleGarageHistories)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_garage_histories_changed_by_user_id_fkey");

            entity.HasOne(d => d.NewGarage).WithMany(p => p.VehicleGarageHistoryNewGarages)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_garage_histories_new_garage_id_fkey");

            entity.HasOne(d => d.OldGarage).WithMany(p => p.VehicleGarageHistoryOldGarages)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_garage_histories_old_garage_id_fkey");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.VehicleGarageHistories)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_garage_histories_vehicle_id_fkey");
        });

        modelBuilder.Entity<VehicleStatus>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("vehicle_statuses_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<VehicleStatusHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("vehicle_status_histories_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.ChangedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.ChangedByUser).WithMany(p => p.VehicleStatusHistories)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_status_histories_changed_by_user_id_fkey");

            entity.HasOne(d => d.Fault).WithMany(p => p.VehicleStatusHistories)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_status_histories_fault_id_fkey");

            entity.HasOne(d => d.NewStatus).WithMany(p => p.VehicleStatusHistoryNewStatuses)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_status_histories_new_status_id_fkey");

            entity.HasOne(d => d.OldStatus).WithMany(p => p.VehicleStatusHistoryOldStatuses)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_status_histories_old_status_id_fkey");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.VehicleStatusHistories)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("vehicle_status_histories_vehicle_id_fkey");
        });

        modelBuilder.Entity<VehicleType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("vehicle_types_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<VwActiveFault>(entity =>
        {
            entity.ToView("vw_active_faults", "fault_management");
        });

        modelBuilder.Entity<VwActiveVehicleDelivery>(entity =>
        {
            entity.ToView("vw_active_vehicle_deliveries", "fault_management");
        });

        modelBuilder.Entity<VwAvailableDriver>(entity =>
        {
            entity.ToView("vw_available_drivers", "fault_management");
        });

        modelBuilder.Entity<VwAvailableTechnicianTeam>(entity =>
        {
            entity.ToView("vw_available_technician_teams", "fault_management");
        });

        modelBuilder.Entity<VwAvailableVehicle>(entity =>
        {
            entity.ToView("vw_available_vehicles", "fault_management");
        });

        modelBuilder.Entity<VwDailyFaultSummary>(entity =>
        {
            entity.ToView("vw_daily_fault_summary", "fault_management");
        });

        modelBuilder.Entity<VwDriverFaultSummary>(entity =>
        {
            entity.ToView("vw_driver_fault_summary", "fault_management");
        });

        modelBuilder.Entity<VwFaultCategorySummary>(entity =>
        {
            entity.ToView("vw_fault_category_summary", "fault_management");
        });

        modelBuilder.Entity<VwFaultRepairDetail>(entity =>
        {
            entity.ToView("vw_fault_repair_details", "fault_management");
        });

        modelBuilder.Entity<VwFaultResolutionTime>(entity =>
        {
            entity.ToView("vw_fault_resolution_times", "fault_management");
        });

        modelBuilder.Entity<VwGarageOccupancy>(entity =>
        {
            entity.ToView("vw_garage_occupancy", "fault_management");
        });

        modelBuilder.Entity<VwGarageVehicleTypeSummary>(entity =>
        {
            entity.ToView("vw_garage_vehicle_type_summary", "fault_management");
        });

        modelBuilder.Entity<VwPendingPasswordReset>(entity =>
        {
            entity.ToView("vw_pending_password_resets", "fault_management");
        });

        modelBuilder.Entity<VwServiceDutySummary>(entity =>
        {
            entity.ToView("vw_service_duty_summary", "fault_management");
        });

        modelBuilder.Entity<VwTaskTransferSummary>(entity =>
        {
            entity.ToView("vw_task_transfer_summary", "fault_management");
        });

        modelBuilder.Entity<VwTasksWaitingForTransfer>(entity =>
        {
            entity.ToView("vw_tasks_waiting_for_transfer", "fault_management");
        });

        modelBuilder.Entity<VwTeamWorkload>(entity =>
        {
            entity.ToView("vw_team_workload", "fault_management");
        });

        modelBuilder.Entity<VwUnreadNotificationCount>(entity =>
        {
            entity.ToView("vw_unread_notification_counts", "fault_management");
        });

        modelBuilder.Entity<VwVehicleCurrentTask>(entity =>
        {
            entity.ToView("vw_vehicle_current_task", "fault_management");
        });

        modelBuilder.Entity<VwVehicleDeliveryHistory>(entity =>
        {
            entity.ToView("vw_vehicle_delivery_history", "fault_management");
        });

        modelBuilder.Entity<VwVehicleFaultSummary>(entity =>
        {
            entity.ToView("vw_vehicle_fault_summary", "fault_management");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
