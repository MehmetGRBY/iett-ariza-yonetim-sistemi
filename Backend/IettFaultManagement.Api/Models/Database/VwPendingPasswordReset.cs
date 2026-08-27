using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Keyless]
public partial class VwPendingPasswordReset
{
    [Column("password_reset_request_id")]
    public long? PasswordResetRequestId { get; set; }

    [Column("user_id")]
    public long? UserId { get; set; }

    [Column("personnel_number")]
    [StringLength(30)]
    public string? PersonnelNumber { get; set; }

    [Column("first_name")]
    [StringLength(100)]
    public string? FirstName { get; set; }

    [Column("last_name")]
    [StringLength(100)]
    public string? LastName { get; set; }

    [Column("request_type")]
    [StringLength(30)]
    public string? RequestType { get; set; }

    [Column("requested_by_user_id")]
    public long? RequestedByUserId { get; set; }

    [Column("requested_at")]
    public DateTime? RequestedAt { get; set; }

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("is_expired")]
    public bool? IsExpired { get; set; }
}
