using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("password_reset_requests", Schema = "fault_management")]
[Index("UserId", "RequestedAt", Name = "ix_password_reset_user_requested", IsDescending = new[] { false, true })]
[Index("TokenHash", Name = "password_reset_requests_token_hash_key", IsUnique = true)]
public partial class PasswordResetRequest
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("user_id")]
    public long UserId { get; set; }

    [Column("request_type")]
    [StringLength(30)]
    public string RequestType { get; set; } = null!;

    [Column("token_hash")]
    [StringLength(128)]
    public string TokenHash { get; set; } = null!;

    [Column("requested_by_user_id")]
    public long? RequestedByUserId { get; set; }

    [Column("requested_ip_address")]
    public IPAddress? RequestedIpAddress { get; set; }

    [Column("requested_at")]
    public DateTime RequestedAt { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("used_at")]
    public DateTime? UsedAt { get; set; }

    [Column("revoked_at")]
    public DateTime? RevokedAt { get; set; }

    [Column("revoke_reason")]
    [StringLength(500)]
    public string? RevokeReason { get; set; }

    [ForeignKey("RequestedByUserId")]
    [InverseProperty("PasswordResetRequestRequestedByUsers")]
    public virtual AppUser? RequestedByUser { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("PasswordResetRequestUsers")]
    public virtual AppUser User { get; set; } = null!;
}
