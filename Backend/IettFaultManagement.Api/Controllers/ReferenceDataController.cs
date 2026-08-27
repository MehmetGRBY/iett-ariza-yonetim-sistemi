using IettFaultManagement.Api.Data;using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using Microsoft.EntityFrameworkCore;
namespace IettFaultManagement.Api.Controllers;
[ApiController,Authorize,Route("api/reference-data")]
/// <summary>Combobox ve filtrelerde kullanılacak kategori, durum, araç tipi ve kök neden tanımlarını verir.</summary>
public sealed class ReferenceDataController(ApplicationDbContext db):ControllerBase
{
 [HttpGet("fault-categories")]public async Task<IActionResult> Categories()=>Ok(await db.FaultCategories.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.ParentCategory!.Name).ThenBy(x=>x.Name).Select(x=>new{x.Id,x.Name,x.ParentCategoryId,Parent=x.ParentCategory!=null?x.ParentCategory.Name:null,x.EstimatedRepairMinutes,x.OnsiteRepairMinutes,x.ResponseSlaMinutes,x.ResolutionSlaMinutes}).ToListAsync());
 [HttpGet("fault-statuses")]public async Task<IActionResult> Statuses()=>Ok(await db.FaultStatuses.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.DisplayOrder).Select(x=>new{x.Id,x.Code,x.Name,x.IsClosedStatus}).ToListAsync());
 [HttpGet("vehicle-types")]public async Task<IActionResult> VehicleTypes()=>Ok(await db.VehicleTypes.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.Name).Select(x=>new{x.Id,x.Name}).ToListAsync());
 [HttpGet("root-causes")]public async Task<IActionResult> RootCauses()=>Ok(await db.RootCauses.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.Name).ToListAsync());
}
