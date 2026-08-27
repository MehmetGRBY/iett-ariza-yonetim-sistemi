using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("system_settings", Schema = "fault_management")]
[Index("SettingKey", Name = "system_settings_setting_key_key", IsUnique = true)]
public partial class SystemSetting
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("setting_key")]
    [StringLength(120)]
    public string SettingKey { get; set; } = null!;

    [Column("setting_value", TypeName = "jsonb")]
    public string SettingValue { get; set; } = null!;

    [Column("description")]
    [StringLength(500)]
    public string? Description { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("updated_by_user_id")]
    public long? UpdatedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey("UpdatedByUserId")]
    [InverseProperty("SystemSettings")]
    public virtual AppUser? UpdatedByUser { get; set; }
}
