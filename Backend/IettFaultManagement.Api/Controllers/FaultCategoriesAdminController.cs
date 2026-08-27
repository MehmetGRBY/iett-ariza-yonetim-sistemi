using System.Text.Json;
using IettFaultManagement.Api.Data;
using IettFaultManagement.Api.Dtos;
using IettFaultManagement.Api.Extensions;
using IettFaultManagement.Api.Models.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Controllers;

/// <summary>
/// Adminin üst ve alt arıza kategorilerini yönetmesini sağlar. Kullanılmış tanımlar silinmez;
/// geçmiş arıza kayıtları korunarak aktif veya pasif duruma alınır.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/fault-categories")]
public sealed class FaultCategoriesAdminController(ApplicationDbContext db) : ControllerBase
{
    /// <summary>Kategorileri hiyerarşi, kullanım ve aktiflik bilgileriyle listeler.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var categories = await db.FaultCategories.AsNoTracking()
            .OrderBy(x => x.ParentCategoryId != null)
            .ThenBy(x => x.ParentCategory != null ? x.ParentCategory.Name : x.Name)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.ParentCategoryId,
                ParentName = x.ParentCategory == null ? null : x.ParentCategory.Name,
                x.IsActive,
                FaultCount = x.Faults.Count,
                ChildCount = x.InverseParentCategory.Count
            })
            .ToListAsync(cancellationToken);

        return Ok(categories);
    }

    /// <summary>ParentCategoryId boşsa üst, doluysa seçilen aktif üst kategoriye bağlı alt kategori oluşturur.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateFaultCategoryRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length < 2) return BadRequest(new { message = "Kategori adı en az 2 karakter olmalıdır." });

        FaultCategory? parent = null;
        if (request.ParentCategoryId.HasValue)
        {
            parent = await db.FaultCategories.SingleOrDefaultAsync(x =>
                x.Id == request.ParentCategoryId && x.ParentCategoryId == null && x.IsActive, cancellationToken);
            if (parent is null) return BadRequest(new { message = "Geçerli ve aktif bir üst kategori seçmelisiniz." });
        }

        var duplicate = await db.FaultCategories.AnyAsync(x => x.ParentCategoryId == request.ParentCategoryId &&
            EF.Functions.ILike(x.Name, name), cancellationToken);
        if (duplicate) return Conflict(new { message = "Aynı seviyede bu kategori adı zaten kullanılıyor." });

        // Database First modelindeki zorunlu operasyon alanları güvenli başlangıç değerleriyle doldurulur.
        var category = new FaultCategory
        {
            Name = name,
            ParentCategoryId = parent?.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            EstimatedRepairMinutes = 60,
            OnsiteRepairMinutes = 20,
            AutoRepairResult = "RESOLVED",
            ResponseSlaMinutes = 15,
            ResolutionSlaMinutes = 240,
            RecurrenceWindowDays = 30,
            RecurrenceAlertCount = 3
        };
        db.FaultCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        AddAudit("FAULT_CATEGORY_CREATED", category.Id, null,
            new { category.Name, category.ParentCategoryId, category.IsActive },
            parent is null ? $"{category.Name} üst arıza kategorisi oluşturuldu."
                : $"{parent.Name} altında {category.Name} alt arıza kategorisi oluşturuldu.");
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = category.Id }, new { category.Id });
    }

    /// <summary>Kategori adını ve aktifliğini değiştirir; üst kategori pasifse altları da pasif yapılır.</summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateFaultCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await db.FaultCategories.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (category is null) return NotFound(new { message = "Arıza kategorisi bulunamadı." });

        var name = request.Name.Trim();
        if (name.Length < 2) return BadRequest(new { message = "Kategori adı en az 2 karakter olmalıdır." });
        if (await db.FaultCategories.AnyAsync(x => x.Id != id && x.ParentCategoryId == category.ParentCategoryId &&
            EF.Functions.ILike(x.Name, name), cancellationToken))
            return Conflict(new { message = "Aynı seviyede bu kategori adı zaten kullanılıyor." });

        if (request.IsActive && category.ParentCategoryId.HasValue &&
            !await db.FaultCategories.AnyAsync(x => x.Id == category.ParentCategoryId && x.IsActive, cancellationToken))
            return BadRequest(new { message = "Pasif bir üst kategoriye bağlı alt kategori aktifleştirilemez." });

        var oldValues = new { category.Name, category.IsActive };
        category.Name = name;
        category.IsActive = request.IsActive;

        // Üst kategori kapatıldığında yeni arıza formlarında hiçbir alt seçeneğin açık kalmaması sağlanır.
        if (!request.IsActive && category.ParentCategoryId == null)
        {
            var children = await db.FaultCategories.Where(x => x.ParentCategoryId == category.Id && x.IsActive)
                .ToListAsync(cancellationToken);
            foreach (var child in children) child.IsActive = false;
        }

        AddAudit("FAULT_CATEGORY_UPDATED", category.Id, oldValues,
            new { category.Name, category.IsActive }, $"{category.Name} arıza kategorisi güncellendi.");
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Kategori değişikliklerini kullanıcı, eski değer ve yeni değer bilgisiyle kaydeder.</summary>
    private void AddAudit(string action, long entityId, object? oldValues, object? newValues, string description)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = User.UserId(),
            Action = action,
            EntityType = "fault_categories",
            EntityId = entityId,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            Description = description,
            CreatedAt = DateTime.UtcNow
        });
    }
}
