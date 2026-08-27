using IettFaultManagement.Api.Data;
using IettFaultManagement.Api.Dtos;
using IettFaultManagement.Api.Extensions;
using IettFaultManagement.Api.Models.Database;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Controllers;

[ApiController, Authorize(Roles = "Admin"), Route("api/admin/users")]
/// <summary>
/// Yalnızca Admin rolünün kullanabildiği personel hesabı yönetim endpoint'lerini içerir.
/// Kullanıcı oluşturma, rol/garaj güncelleme, pasifleştirme ve hesap kilidi açma burada yapılır.
/// </summary>
public sealed class AdminUsersController(
    ApplicationDbContext db,
    IPasswordHasher<AppUser> hasher) : ControllerBase
{
    private const string PasswordNotCreated = "DEMO_ACCOUNT_NOT_ACTIVATED";

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await db.AppUsers.AsNoTracking()
        // Kullanıcı Yönetimi yalnızca uygulamada oturum açabilen hesapları listeler.
        // Teknisyenler operasyon personelidir ve Teknik Ekipler ekranından yönetilir.
        .Where(x => x.Role.Name == "Admin" ||
                    x.Role.Name == "Merkez Yetkilisi" ||
                    x.Role.Name == "Garaj Yetkilisi")
        .OrderBy(x => x.PersonnelNumber)
        .Select(x => new
        {
            x.Id, x.PersonnelNumber, x.FirstName, x.LastName, x.Email,
            Role = new { x.Role.Id, x.Role.Name }, x.GarageId,
            Garage = x.Garage != null ? x.Garage.Name : null,
            x.GenderCode, x.IsActive, x.CreatedAt, x.LastLoginAt, x.LockedUntil, x.FailedLoginCount,
            HasPassword = x.PasswordHash != PasswordNotCreated && x.PasswordHash != ""
        }).ToListAsync());

    [HttpGet("roles")]
    public async Task<IActionResult> Roles() => Ok(await db.Roles.AsNoTracking()
        .Where(x => x.IsActive && new[] { "Admin", "Merkez Yetkilisi", "Garaj Yetkilisi" }.Contains(x.Name))
        .Select(x => new { x.Id, x.Name }).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        var role = await GetAndValidateRoleAsync(request.RoleId, request.GarageId);
        if (role.Result is not null) return role.Result;

        var personnelNumber = string.IsNullOrWhiteSpace(request.PersonnelNumber)
            ? await GeneratePersonnelNumberAsync(role.Role!, request.GarageId)
            : request.PersonnelNumber.Trim().ToUpperInvariant();
        if (await db.AppUsers.AnyAsync(x => x.NormalizedPersonnelNumber == personnelNumber))
            return Conflict(new { message = "Bu sicil numarası zaten kayıtlıdır." });

        var user = new AppUser
        {
            PersonnelNumber = personnelNumber,
            NormalizedPersonnelNumber = personnelNumber,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            RoleId = role.Role!.Id,
            GarageId = role.Role.Name == "Garaj Yetkilisi" ? request.GarageId : null,
            GenderCode = request.GenderCode,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid(),
            // Admin parola belirlemez. Personel login ekranındaki "İlk Kez Giriş"
            // bölümünden sicilini kullanarak kendi parolasını oluşturur.
            PasswordHash = PasswordNotCreated
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        await AddAuditAsync("USER_CREATED", user.Id, null, new { user.PersonnelNumber, user.RoleId, user.GarageId, user.IsActive });
        await db.SaveChangesAsync();
        return Created($"/api/admin/users/{user.Id}", new { user.Id, user.PersonnelNumber });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateUserRequest request)
    {
        var user = await db.AppUsers.Include(x => x.Role).SingleOrDefaultAsync(x => x.Id == id);
        if (user is null) return NotFound();
        if (user.Id == User.UserId() && (!request.IsActive || request.RoleId != user.RoleId))
            return BadRequest(new { message = "Kendi hesabınızın rolünü değiştiremez veya hesabınızı pasife alamazsınız." });
        var role = await GetAndValidateRoleAsync(request.RoleId, request.GarageId, id);
        if (role.Result is not null) return role.Result;
        if (user.Role.Name == "Admin" && (!request.IsActive || role.Role!.Name != "Admin"))
            return BadRequest(new { message = "Admin hesabı pasife alınamaz veya başka role geçirilemez." });

        var oldValues = new { user.FirstName, user.LastName, user.RoleId, user.GarageId, user.GenderCode, user.IsActive, user.PersonnelNumber };
        var roleOrGarageChanged = user.RoleId != role.Role!.Id ||
            (role.Role.Name == "Garaj Yetkilisi" && user.GarageId != request.GarageId);
        if (roleOrGarageChanged)
        {
            var personnelNumber = await GeneratePersonnelNumberAsync(role.Role, request.GarageId);
            user.PersonnelNumber = personnelNumber;
            user.NormalizedPersonnelNumber = personnelNumber;
            user.SecurityStamp = Guid.NewGuid();
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.GenderCode = request.GenderCode;
        user.RoleId = role.Role.Id;
        user.GarageId = role.Role.Name == "Garaj Yetkilisi" ? request.GarageId : null;
        user.IsActive = request.IsActive;
        user.DeactivatedAt = request.IsActive ? null : DateTime.UtcNow;
        user.SecurityStamp = Guid.NewGuid();
        await AddAuditAsync("USER_UPDATED", user.Id, oldValues,
            new { user.FirstName, user.LastName, user.RoleId, user.GarageId, user.GenderCode, user.IsActive, user.PersonnelNumber });
        await db.SaveChangesAsync();
        return Ok(new { user.Id, user.PersonnelNumber, user.IsActive });
    }

    [HttpPut("{id:long}/active")]
    public async Task<IActionResult> Toggle(long id)
    {
        var user = await db.AppUsers.Include(x => x.Role).SingleOrDefaultAsync(x => x.Id == id);
        if (user is null) return NotFound();
        if (user.Role.Name == "Admin") return BadRequest(new { message = "Admin hesabı pasife alınamaz." });

        // Pasif bir garaj yetkilisi yeniden aktifleştirilirken aynı garajdaki mevcut aktif
        // yetkili kontrol edilir. Oluşturma/düzenleme akışındaki iş kuralı burada da korunur.
        if (!user.IsActive && user.Role.Name == "Garaj Yetkilisi" && user.GarageId.HasValue &&
            await db.AppUsers.AnyAsync(x => x.Id != user.Id && x.IsActive &&
                x.Role.Name == "Garaj Yetkilisi" && x.GarageId == user.GarageId))
            return Conflict(new
            {
                message = "Bu garajda zaten aktif bir garaj yetkilisi bulunmaktadır. Mevcut yetkili pasife alınmadan bu hesap aktifleştirilemez."
            });

        user.IsActive = !user.IsActive;
        user.SecurityStamp = Guid.NewGuid();
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        if (!user.IsActive) user.DeactivatedAt = DateTime.UtcNow;
        else { user.DeactivatedAt = null; user.DeactivationReason = null; }
        await AddAuditAsync(user.IsActive ? "USER_ACTIVATED" : "USER_DEACTIVATED", user.Id,
            new { IsActive = !user.IsActive }, new { user.IsActive });
        await db.SaveChangesAsync();
        return Ok(new { user.Id, user.IsActive });
    }

    [HttpPut("{id:long}/unlock")]
    public async Task<IActionResult> Unlock(long id)
    {
        var user = await db.AppUsers.FindAsync(id);
        if (user is null) return NotFound();
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.SecurityStamp = Guid.NewGuid();
        await AddAuditAsync("USER_UNLOCKED", user.Id, null, new { LockedUntil = (DateTime?)null });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:long}/password")]
    public async Task<IActionResult> ResetPassword(long id, ResetUserPasswordRequest request)
    {
        var user = await db.AppUsers.FindAsync(id);
        if (user is null) return NotFound();
        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.SecurityStamp = Guid.NewGuid();
        await AddAuditAsync("USER_PASSWORD_RESET", user.Id, null, new { PasswordReset = true });
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task AddAuditAsync(string action, long entityId, object? oldValues, object? newValues)
    {
        var actorId = User.UserId();
        var roleId = await db.AppUsers.Where(x => x.Id == actorId).Select(x => x.RoleId).SingleAsync();
        db.AuditLogs.Add(new AuditLog
        {
            UserId = actorId,
            RoleId = roleId,
            Action = action,
            EntityType = "app_users",
            EntityId = entityId,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            Description = "Admin kullanıcı yönetimi işlemi.",
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task<(Role? Role, IActionResult? Result)> GetAndValidateRoleAsync(
        long? roleId, long? garageId, long? excludedUserId = null)
    {
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Id == roleId && x.IsActive);
        if (role is null || role.Name is not ("Admin" or "Merkez Yetkilisi" or "Garaj Yetkilisi"))
            return (null, BadRequest(new { message = "Geçerli rol seçin." }));
        if (role.Name == "Garaj Yetkilisi" && !garageId.HasValue)
            return (null, BadRequest(new { message = "Garaj yetkilisi için garaj zorunludur." }));
        if (role.Name == "Garaj Yetkilisi" && !await db.Garages.AnyAsync(x => x.Id == garageId && x.IsActive))
            return (null, BadRequest(new { message = "Geçerli ve aktif bir garaj seçin." }));
        if (role.Name == "Garaj Yetkilisi" && await db.AppUsers.AnyAsync(x =>
                x.Id != excludedUserId && x.IsActive && x.Role.Name == "Garaj Yetkilisi" && x.GarageId == garageId))
            return (null, Conflict(new { message = "Bu garajda zaten aktif yetkili var." }));
        return (role, null);
    }

    private async Task<string> GeneratePersonnelNumberAsync(Role role, long? garageId)
    {
        var prefix = role.Name switch
        {
            "Admin" => "ADM",
            "Merkez Yetkilisi" => "MRK",
            "Garaj Yetkilisi" => $"GRJ-{await db.Garages.Where(x => x.Id == garageId).Select(x => x.Code).SingleAsync()}",
            _ => throw new InvalidOperationException("Sicil numarası oluşturulamadı.")
        };
        var numbers = await db.AppUsers.AsNoTracking()
            .Where(x => x.PersonnelNumber.StartsWith(prefix + "-"))
            .Select(x => x.PersonnelNumber)
            .ToListAsync();
        var next = numbers.Select(x => int.TryParse(x.Split('-').Last(), out var value) ? value : 0)
            .DefaultIfEmpty().Max() + 1;
        return $"{prefix}-{next:000}";
    }
}
