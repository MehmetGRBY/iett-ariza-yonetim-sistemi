using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Keyless]
public partial class VwFaultRepairDetail
{
    [Column("fault_id")]
    public long? FaultId { get; set; }

    [Column("fault_number")]
    [StringLength(40)]
    public string? FaultNumber { get; set; }

    [Column("vehicle_id")]
    public long? VehicleId { get; set; }

    [Column("door_number")]
    [StringLength(30)]
    public string? DoorNumber { get; set; }

    [Column("plate")]
    [StringLength(20)]
    public string? Plate { get; set; }

    [Column("garage_id")]
    public long? GarageId { get; set; }

    [Column("garage_name")]
    [StringLength(150)]
    public string? GarageName { get; set; }

    [Column("fault_status_code")]
    [StringLength(50)]
    public string? FaultStatusCode { get; set; }

    [Column("fault_status_name")]
    [StringLength(80)]
    public string? FaultStatusName { get; set; }

    [Column("fault_assignment_id")]
    public long? FaultAssignmentId { get; set; }

    [Column("team_id")]
    public long? TeamId { get; set; }

    [Column("team_name")]
    [StringLength(120)]
    public string? TeamName { get; set; }

    [Column("assigned_at")]
    public DateTime? AssignedAt { get; set; }

    [Column("assignment_started_at")]
    public DateTime? AssignmentStartedAt { get; set; }

    [Column("assignment_completed_at")]
    public DateTime? AssignmentCompletedAt { get; set; }

    [Column("repair_report_id")]
    public long? RepairReportId { get; set; }

    [Column("repair_result")]
    [StringLength(30)]
    public string? RepairResult { get; set; }

    [Column("repair_description")]
    public string? RepairDescription { get; set; }

    [Column("repair_started_at")]
    public DateTime? RepairStartedAt { get; set; }

    [Column("repair_completed_at")]
    public DateTime? RepairCompletedAt { get; set; }

    [Column("submitted_at")]
    public DateTime? SubmittedAt { get; set; }

    [Column("is_submitted")]
    public bool? IsSubmitted { get; set; }

    [Column("action_count")]
    public long? ActionCount { get; set; }

    [Column("action_descriptions")]
    public string? ActionDescriptions { get; set; }

    [Column("part_line_count")]
    public long? PartLineCount { get; set; }

    [Column("part_descriptions")]
    public string? PartDescriptions { get; set; }

    [Column("attachment_count")]
    public long? AttachmentCount { get; set; }
}
