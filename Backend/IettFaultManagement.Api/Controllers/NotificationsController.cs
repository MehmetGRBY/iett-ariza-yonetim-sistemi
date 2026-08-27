using IettFaultManagement.Api.Data;using IettFaultManagement.Api.Extensions;using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using Microsoft.EntityFrameworkCore;
namespace IettFaultManagement.Api.Controllers;
[ApiController,Authorize,Route("api/notifications")]
/// <summary>Oturum açan kullanıcının bildirimlerini listeler ve tekil/toplu okundu durumunu günceller.</summary>
public sealed class NotificationsController(ApplicationDbContext db):ControllerBase
{
 [HttpGet]public async Task<IActionResult> Get([FromQuery]bool unreadOnly=false){var q=db.Notifications.AsNoTracking().Where(x=>x.UserId==User.UserId());if(unreadOnly)q=q.Where(x=>!x.IsRead);return Ok(await q.OrderByDescending(x=>x.CreatedAt).Take(200).Select(x=>new{x.Id,x.Title,x.Message,x.NotificationType,x.IsRead,x.CreatedAt,x.ReadAt,x.FaultId,x.ServiceTaskId}).ToListAsync());}
 [HttpPut("{id:long}/read")]public async Task<IActionResult> Read(long id){var n=await db.Notifications.SingleOrDefaultAsync(x=>x.Id==id&&x.UserId==User.UserId());if(n is null)return NotFound();n.IsRead=true;n.ReadAt=DateTime.UtcNow;await db.SaveChangesAsync();return NoContent();}
 [HttpPut("read-all")]public async Task<IActionResult> ReadAll(){var items=await db.Notifications.Where(x=>x.UserId==User.UserId()&&!x.IsRead).ToListAsync();foreach(var x in items){x.IsRead=true;x.ReadAt=DateTime.UtcNow;}await db.SaveChangesAsync();return NoContent();}
}
