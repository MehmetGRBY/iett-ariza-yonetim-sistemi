using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Table("repair_report_attachments", Schema = "fault_management")]
[Index("StoredFileName", Name = "repair_report_attachments_stored_file_name_key", IsUnique = true)]
public partial class RepairReportAttachment
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("repair_report_id")]
    public long RepairReportId { get; set; }

    [Column("original_file_name")]
    [StringLength(255)]
    public string OriginalFileName { get; set; } = null!;

    [Column("stored_file_name")]
    [StringLength(255)]
    public string StoredFileName { get; set; } = null!;

    [Column("file_path")]
    [StringLength(1000)]
    public string FilePath { get; set; } = null!;

    [Column("content_type")]
    [StringLength(150)]
    public string ContentType { get; set; } = null!;

    [Column("file_size")]
    public long FileSize { get; set; }

    [Column("uploaded_by_user_id")]
    public long UploadedByUserId { get; set; }

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [ForeignKey("RepairReportId")]
    [InverseProperty("RepairReportAttachments")]
    public virtual RepairReport RepairReport { get; set; } = null!;

    [ForeignKey("UploadedByUserId")]
    [InverseProperty("RepairReportAttachments")]
    public virtual AppUser UploadedByUser { get; set; } = null!;
}
