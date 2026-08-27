using IettFaultManagement.Api.Data;
using IettFaultManagement.Api.Dtos;
using IettFaultManagement.Api.Extensions;
using IettFaultManagement.Api.Models.Database;
using IettFaultManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IettFaultManagement.Api.Controllers;
[ApiController,Route("api/system")]
/// <summary>Uygulamanın PostgreSQL'e bağlanabildiğini kontrol eden teknik sağlık endpoint'ini sunar.</summary>
public sealed class SystemController(ApplicationDbContext db,PageAccessService pageAccess):ControllerBase
{
 [AllowAnonymous,HttpGet("database-health")]
 public async Task<IActionResult> DatabaseHealth(CancellationToken token)
 {
  try{return Ok(new{status="Healthy",database=await db.Database.CanConnectAsync(token),utcTime=DateTime.UtcNow});}
 catch{return StatusCode(503,new{status="Unhealthy",database=false,utcTime=DateTime.UtcNow});}
 }

 /// <summary>Oturumdaki rolün menüde ve doğrudan sayfa açılışında kullanabileceği sayfa anahtarlarını döndürür.</summary>
 [Authorize,HttpGet("page-access")]
 public async Task<IActionResult> PageAccess(CancellationToken token)
 {
  var role=User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value??string.Empty;
  return Ok(new{allowedPages=await pageAccess.GetAllowedPagesAsync(role,token)});
 }

 /// <summary>Adminin yönetebileceği tanımlı sistem ayarlarını son güncelleyen kullanıcıyla listeler.</summary>
 [Authorize(Roles="Admin"),HttpGet("settings")]
 public async Task<IActionResult> Settings()=>Ok(await db.SystemSettings.AsNoTracking()
  // Çalışma modu artık seçilebilir bir ayar değildir; sistem daima yarı otomatik çalışır.
  .Where(x=>x.SettingKey!="fault_operation_mode"&&
            x.SettingKey!="automatic_team_assignment"&&
            x.SettingKey!="automatic_replacement_vehicle_assignment")
  .OrderBy(x=>x.SettingKey).Select(x=>new{x.Id,x.SettingKey,x.SettingValue,x.Description,x.IsActive,
   x.UpdatedAt,UpdatedBy=x.UpdatedByUser==null?null:x.UpdatedByUser.FirstName+" "+x.UpdatedByUser.LastName}).ToListAsync());

 /// <summary>Mevcut ayarın JSON değerini doğrular, kaydeder ve değişikliği denetim günlüğüne ekler.</summary>
 [Authorize(Roles="Admin"),HttpPut("settings/{id:long}")]
 public async Task<IActionResult> UpdateSetting(long id,UpdateSystemSettingRequest request)
 {
  var setting=await db.SystemSettings.FindAsync(id);
  if(setting is null)return NotFound();
  // Eski kayıt veritabanında geçmiş uyumluluğu için durabilir; çalışma biçimi
  // artık kullanıcı tarafından değiştirilemez ve daima yarı otomatiktir.
  if(setting.SettingKey is "fault_operation_mode" or "automatic_team_assignment" or "automatic_replacement_vehicle_assignment")
   return BadRequest(new{message="Bu operasyon davranışı sistem akışında sabittir ve ayar olarak değiştirilemez."});
  try
  {
   using var document=JsonDocument.Parse(request.SettingValue);
   var validationError=ValidateSettingValue(setting.SettingKey,document.RootElement);
   if(validationError is not null)return BadRequest(new{message=validationError});
   if(setting.SettingKey==PageAccessService.SettingKey)
    request=request with{SettingValue=JsonSerializer.Serialize(PageAccessService.NormalizeMatrix(request.SettingValue))};
  }
  catch(JsonException){return BadRequest(new{message="Ayar değeri geçerli JSON biçiminde olmalıdır. Metin değerleri çift tırnak içinde yazılmalıdır."});}
  var now=DateTime.UtcNow;var oldValue=setting.SettingValue;
  setting.SettingValue=request.SettingValue.Trim();setting.Description=request.Description?.Trim();
  setting.IsActive=request.IsActive;setting.UpdatedByUserId=User.UserId();setting.UpdatedAt=now;
  db.AuditLogs.Add(new AuditLog{UserId=User.UserId(),Action="SYSTEM_SETTING_UPDATED",EntityType="system_settings",
   EntityId=setting.Id,OldValues=JsonSerializer.Serialize(new{setting.SettingKey,SettingValue=oldValue}),
   NewValues=JsonSerializer.Serialize(new{setting.SettingKey,setting.SettingValue,setting.IsActive}),
   Description=$"{setting.SettingKey} sistem ayarı güncellendi.",CreatedAt=now});
  await db.SaveChangesAsync();return NoContent();
 }

 private static string? ValidateSettingValue(string key,JsonElement value)
 {
  var numericRanges=new Dictionary<string,(int Minimum,int Maximum)>
  {
   ["failed_login_limit"]=(1,20),
   ["account_lock_minutes"]=(1,1440),
   ["presentation_dispatch_seconds"]=(1,3600),
   ["presentation_repair_seconds"]=(1,3600),
   ["max_post_repair_inspection_attempts"]=(1,10),
   ["open_fault_alert_hours"]=(1,168)
  };
  if(numericRanges.TryGetValue(key,out var range)&&
     (!value.TryGetInt32(out var number)||number<range.Minimum||number>range.Maximum))
   return $"Değer {range.Minimum} ile {range.Maximum} arasında tam sayı olmalıdır.";
  return null;
 }
}
