using IettFaultManagement.Api.Data;
using IettFaultManagement.Api.Dtos;
using IettFaultManagement.Api.Extensions;
using IettFaultManagement.Api.Models.Database;
using IettFaultManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IettFaultManagement.Api.Controllers;
[ApiController,Route("api/auth")]
/// <summary>
/// Sicil numarası/parola ile giriş, mevcut oturum bilgisi ve parola değiştirme işlemlerini sunar.
/// Başarısız denemeleri sayar, hesabı geçici kilitler ve başarılı girişte JWT üretir.
/// </summary>
public sealed class AuthController(ApplicationDbContext db,IPasswordHasher<AppUser> hasher,JwtTokenService tokens):ControllerBase
{
 private const string PasswordNotCreated="DEMO_ACCOUNT_NOT_ACTIVATED";

 [AllowAnonymous,HttpPost("login")]
 public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
 {
  var normalized=request.PersonnelNumber.Trim().ToUpperInvariant();
  var user=await db.AppUsers.Include(x=>x.Role).Include(x=>x.Garage).SingleOrDefaultAsync(x=>x.NormalizedPersonnelNumber==normalized);
  if(user is null||!user.IsActive||user.Role.Name is not ("Admin" or "Merkez Yetkilisi" or "Garaj Yetkilisi"))return Unauthorized(new{message="Sicil numarası veya parola hatalıdır."});
  if(user.PasswordHash==PasswordNotCreated)return Unauthorized(new{message="Bu hesap için henüz parola oluşturulmamış. İlk Kez Giriş bölümünü kullanın."});
  var now=DateTime.UtcNow;if(user.LockedUntil>now)return StatusCode(423,new{message="Hesap geçici olarak kilitlidir.",user.LockedUntil});
  var result=hasher.VerifyHashedPassword(user,user.PasswordHash,request.Password);
  if(result==PasswordVerificationResult.Failed)
  {
   // Güvenlik politikası kaynak kodda sabit tutulmaz; Sistem Ayarları ekranından yönetilir.
   var failedLimit=await ReadIntegerSettingAsync("failed_login_limit",5,1,20);
   var lockMinutes=await ReadIntegerSettingAsync("account_lock_minutes",15,1,1440);
   user.FailedLoginCount++;
   if(user.FailedLoginCount>=failedLimit)user.LockedUntil=now.AddMinutes(lockMinutes);
   await db.SaveChangesAsync();
   return Unauthorized(new{message="Sicil numarası veya parola hatalıdır."});
  }
  user.FailedLoginCount=0;user.LockedUntil=null;user.LastLoginAt=now;
  if(result==PasswordVerificationResult.SuccessRehashNeeded)user.PasswordHash=hasher.HashPassword(user,request.Password);
  await db.SaveChangesAsync();var token=tokens.Create(user);
  return Ok(new LoginResponse(token.Token,token.ExpiresAt,new UserResponse(user.Id,user.PersonnelNumber,$"{user.FirstName} {user.LastName}",user.Role.Name,user.GarageId,user.Garage?.Name)));
 }

 [AllowAnonymous,HttpPost("activate")]
 public async Task<IActionResult> Activate(ActivateAccountRequest request)
 {
  if(request.NewPassword!=request.ConfirmPassword)return BadRequest(new{message="Yeni parola ile parola tekrarı eşleşmiyor."});
  var normalized=request.PersonnelNumber.Trim().ToUpperInvariant();
  var user=await db.AppUsers.Include(x=>x.Role).SingleOrDefaultAsync(x=>x.NormalizedPersonnelNumber==normalized);
  if(user is null||!user.IsActive||user.Role.Name is not ("Admin" or "Merkez Yetkilisi" or "Garaj Yetkilisi"))
   return BadRequest(new{message="Aktif ve yetkili bir personel kaydı bulunamadı."});
  if(user.PasswordHash!=PasswordNotCreated)
   return Conflict(new{message="Bu hesap için daha önce parola oluşturulmuş. Giriş Yap veya Parola Değiştir bölümünü kullanın."});

  user.PasswordHash=hasher.HashPassword(user,request.NewPassword);
  user.PasswordChangedAt=DateTime.UtcNow;user.FailedLoginCount=0;user.LockedUntil=null;user.SecurityStamp=Guid.NewGuid();
  AddAudit(user,"USER_PASSWORD_CREATED","Personel ilk giriş parolasını oluşturdu.");
  await db.SaveChangesAsync();
  return Ok(new{message="Parolanız oluşturuldu. Artık giriş yapabilirsiniz."});
 }

 [AllowAnonymous,HttpPut("change-password")]
 public async Task<IActionResult> ChangePasswordFromLogin(PublicChangePasswordRequest request)
 {
  if(request.NewPassword!=request.ConfirmPassword)return BadRequest(new{message="Yeni parola ile parola tekrarı eşleşmiyor."});
  if(request.CurrentPassword==request.NewPassword)return BadRequest(new{message="Yeni parola mevcut paroladan farklı olmalıdır."});

  var normalized=request.PersonnelNumber.Trim().ToUpperInvariant();
  var user=await db.AppUsers.Include(x=>x.Role).SingleOrDefaultAsync(x=>x.NormalizedPersonnelNumber==normalized);
  if(user is null||!user.IsActive||user.Role.Name is not ("Admin" or "Merkez Yetkilisi" or "Garaj Yetkilisi"))
   return Unauthorized(new{message="Sicil numarası veya mevcut parola hatalıdır."});
  if(user.PasswordHash==PasswordNotCreated)return BadRequest(new{message="Önce İlk Kez Giriş bölümünden parolanızı oluşturun."});

  var now=DateTime.UtcNow;
  if(user.LockedUntil>now)return StatusCode(423,new{message="Hesap geçici olarak kilitlidir.",user.LockedUntil});
  if(hasher.VerifyHashedPassword(user,user.PasswordHash,request.CurrentPassword)==PasswordVerificationResult.Failed)
  {
   var failedLimit=await ReadIntegerSettingAsync("failed_login_limit",5,1,20);
   var lockMinutes=await ReadIntegerSettingAsync("account_lock_minutes",15,1,1440);
   user.FailedLoginCount++;
   if(user.FailedLoginCount>=failedLimit)user.LockedUntil=now.AddMinutes(lockMinutes);
   await db.SaveChangesAsync();
   return Unauthorized(new{message="Sicil numarası veya mevcut parola hatalıdır."});
  }

  user.PasswordHash=hasher.HashPassword(user,request.NewPassword);
  user.PasswordChangedAt=now;user.FailedLoginCount=0;user.LockedUntil=null;user.SecurityStamp=Guid.NewGuid();
  AddAudit(user,"USER_PASSWORD_CHANGED","Personel parolasını login ekranından değiştirdi.");
  await db.SaveChangesAsync();
  return Ok(new{message="Parolanız değiştirildi. Yeni parolanızla giriş yapabilirsiniz."});
 }
 [Authorize,HttpGet("me")]
 public async Task<ActionResult<UserResponse>> Me()=>await db.AppUsers.Where(x=>x.Id==User.UserId()).Select(x=>new UserResponse(x.Id,x.PersonnelNumber,x.FirstName+" "+x.LastName,x.Role.Name,x.GarageId,x.Garage!=null?x.Garage.Name:null)).SingleAsync();
 [Authorize,HttpPut("password")]
 public async Task<IActionResult> ChangePassword(ChangePasswordRequest request){var user=await db.AppUsers.FindAsync(User.UserId());if(user is null)return Unauthorized();if(hasher.VerifyHashedPassword(user,user.PasswordHash,request.CurrentPassword)==PasswordVerificationResult.Failed)return BadRequest(new{message="Mevcut parola hatalıdır."});user.PasswordHash=hasher.HashPassword(user,request.NewPassword);user.PasswordChangedAt=DateTime.UtcNow;user.SecurityStamp=Guid.NewGuid();await db.SaveChangesAsync();return NoContent();}

 private async Task<int> ReadIntegerSettingAsync(string key,int fallback,int minimum,int maximum)
 {
  var json=await db.SystemSettings.AsNoTracking().Where(x=>x.SettingKey==key&&x.IsActive)
   .Select(x=>x.SettingValue).SingleOrDefaultAsync();
  return int.TryParse(json,out var value)?Math.Clamp(value,minimum,maximum):fallback;
 }

 // Parola işlemleri kullanıcı tarafından yapılsa da denetlenebilirlik için işlem günlüğüne yazılır.
 private void AddAudit(AppUser user,string action,string description)=>db.AuditLogs.Add(new AuditLog
 {
  UserId=user.Id,RoleId=user.RoleId,Action=action,EntityType="app_users",EntityId=user.Id,
  NewValues=JsonSerializer.Serialize(new{PasswordChanged=true}),Description=description,
  IpAddress=HttpContext.Connection.RemoteIpAddress,CreatedAt=DateTime.UtcNow
 });
}
