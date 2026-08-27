using IettFaultManagement.Api.Data;
using IettFaultManagement.Api.Extensions;
using IettFaultManagement.Api.Models.Database;
using IettFaultManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Controllers;

[ApiController, Authorize(Roles = "Admin,Merkez Yetkilisi,Garaj Yetkilisi"), Route("api")]
/// <summary>
/// Arıza ve tamir raporu eklerini güvenli depolama servisi üzerinden yükler/indirir;
/// kaydın garaj kapsamını ve izin verilen dosya türü/boyutunu denetler.
/// </summary>
public sealed class AttachmentsController(
    ApplicationDbContext db,
    AttachmentStorageService storage) : ControllerBase
{
    [HttpPost("faults/{faultId:long}/attachments")]
    [RequestSizeLimit(AttachmentStorageService.MaximumFileSize)]
    public async Task<IActionResult> UploadFaultAttachment(
        long faultId, IFormFile file, CancellationToken cancellationToken)
    {
        var fault = await db.Faults.AsNoTracking().SingleOrDefaultAsync(x => x.Id == faultId, cancellationToken);
        if (fault is null) return NotFound();
        if (User.IsInRole("Garaj Yetkilisi") && fault.GarageId != User.GarageId()) return Forbid();

        var stored = await storage.SaveAsync(file, "faults", faultId, cancellationToken);
        try
        {
            var attachment = new FaultAttachment
            {
                FaultId = faultId,
                OriginalFileName = stored.OriginalFileName,
                StoredFileName = stored.StoredFileName,
                FilePath = stored.RelativePath,
                ContentType = stored.ContentType,
                FileSize = stored.FileSize,
                UploadedByUserId = User.UserId(),
                UploadedAt = DateTime.UtcNow,
                IsActive = true
            };
            db.FaultAttachments.Add(attachment);
            await db.SaveChangesAsync(cancellationToken);
            return Created($"/api/fault-attachments/{attachment.Id}", new
            {
                attachment.Id, attachment.OriginalFileName, attachment.ContentType, attachment.FileSize
            });
        }
        catch
        {
            storage.DeleteIfExists(stored.RelativePath);
            throw;
        }
    }

    [HttpGet("fault-attachments/{id:long}")]
    public async Task<IActionResult> DownloadFaultAttachment(long id, CancellationToken cancellationToken)
    {
        var attachment = await db.FaultAttachments.AsNoTracking()
            .Where(x => x.Id == id && x.IsActive)
            .Select(x => new { x.FilePath, x.OriginalFileName, x.ContentType, x.Fault.GarageId })
            .SingleOrDefaultAsync(cancellationToken);
        if (attachment is null) return NotFound();
        if (User.IsInRole("Garaj Yetkilisi") && attachment.GarageId != User.GarageId()) return Forbid();
        return File(storage.OpenRead(attachment.FilePath), attachment.ContentType, attachment.OriginalFileName);
    }

    [Authorize(Roles = "Admin,Garaj Yetkilisi"), HttpPost("repair-reports/{reportId:long}/attachments")]
    [RequestSizeLimit(AttachmentStorageService.MaximumFileSize)]
    public async Task<IActionResult> UploadReportAttachment(
        long reportId, IFormFile file, CancellationToken cancellationToken)
    {
        var report = await db.RepairReports.AsNoTracking()
            .Where(x => x.Id == reportId && x.IsActive)
            .Select(x => new { x.Id, x.FaultAssignment.Fault.GarageId })
            .SingleOrDefaultAsync(cancellationToken);
        if (report is null) return NotFound();
        if (User.IsInRole("Garaj Yetkilisi") && report.GarageId != User.GarageId()) return Forbid();

        var stored = await storage.SaveAsync(file, "repair-reports", reportId, cancellationToken);
        try
        {
            var attachment = new RepairReportAttachment
            {
                RepairReportId = reportId,
                OriginalFileName = stored.OriginalFileName,
                StoredFileName = stored.StoredFileName,
                FilePath = stored.RelativePath,
                ContentType = stored.ContentType,
                FileSize = stored.FileSize,
                UploadedByUserId = User.UserId(),
                UploadedAt = DateTime.UtcNow,
                IsActive = true
            };
            db.RepairReportAttachments.Add(attachment);
            await db.SaveChangesAsync(cancellationToken);
            return Created($"/api/repair-report-attachments/{attachment.Id}", new { attachment.Id });
        }
        catch
        {
            storage.DeleteIfExists(stored.RelativePath);
            throw;
        }
    }
}
