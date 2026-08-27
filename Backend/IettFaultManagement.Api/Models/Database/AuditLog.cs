using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("audit_logs", Schema = "fault_management")]
[Index("EntityType", "EntityId", "CreatedAt", Name = "ix_audit_logs_entity", IsDescending = new[] { false, false, true })]
[Index("UserId", "CreatedAt", Name = "ix_audit_logs_user_created", IsDescending = new[] { false, true })]
public partial class AuditLog
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("user_id")]
    public long? UserId { get; set; }

    [Column("role_id")]
    public long? RoleId { get; set; }

    [Column("action")]
    [StringLength(50)]
    public string Action { get; set; } = null!;

    [Column("entity_type")]
    [StringLength(120)]
    public string EntityType { get; set; } = null!;

    [Column("entity_id")]
    public long? EntityId { get; set; }

    [Column("old_values", TypeName = "jsonb")]
    public string? OldValues { get; set; }

    [Column("new_values", TypeName = "jsonb")]
    public string? NewValues { get; set; }

    [Column("description")]
    [StringLength(1000)]
    public string? Description { get; set; }

    [Column("ip_address")]
    public IPAddress? IpAddress { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("RoleId")]
    [InverseProperty("AuditLogs")]
    public virtual Role? Role { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("AuditLogs")]
    public virtual AppUser? User { get; set; }
}
