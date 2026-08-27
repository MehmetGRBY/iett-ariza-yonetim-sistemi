using IettFaultManagement.Api.Data;using IettFaultManagement.Api.Extensions;using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using Microsoft.EntityFrameworkCore;
namespace IettFaultManagement.Api.Controllers;
[ApiController,Authorize(Roles="Admin,Merkez Yetkilisi,Garaj Yetkilisi"),Route("api/monitoring")]
/// <summary>SLA ihlali, tekrar eden arıza ve araç sağlık göstergelerini operasyon ekranına sunar.</summary>
public sealed class MonitoringController(ApplicationDbContext db):ControllerBase
{
 // SLA kaydı, ekranda doğrudan kullanılabilmesi için araç ve garaj adlarıyla zenginleştirilir.
 [HttpGet("sla")]
 public async Task<IActionResult> Sla()
 {
  var query=from sla in db.VwFaultSlaStatuses.AsNoTracking()
            join vehicle in db.Vehicles.AsNoTracking() on sla.VehicleId equals vehicle.Id
            select new{sla,vehicle};
  if(User.IsInRole("Garaj Yetkilisi"))query=query.Where(x=>x.sla.GarageId==User.GarageId());
  return Ok(await query.OrderBy(x=>x.sla.ResolutionDueAt).Take(500).Select(x=>new
  {
   x.sla.FaultId,x.sla.FaultNumber,x.vehicle.DoorNumber,x.vehicle.Plate,
   Garage=x.vehicle.Garage.Name,x.sla.CreatedAt,x.sla.FirstResponseAt,x.sla.ClosedAt,
   x.sla.ResponseDueAt,x.sla.ResolutionDueAt,x.sla.SlaStatus
  }).ToListAsync());
 }

 // Tekrarlayan arızalarda sayısal kategori kimliği yerine personelin okuyacağı kategori adı döner.
 [HttpGet("recurring-faults")]
 public async Task<IActionResult> Recurring()
 {
  // Operasyon ekranı, kategori alarm eşiğinden bağımsız olarak son 90 günde aynı
  // araç ve aynı alt kategoride en az iki kez oluşan arızayı "tekrarlayan" kabul eder.
  // Alarm worker'ı ise kategoriye özel daha yüksek eşiği kullanan database view'ine devam eder.
  var since=DateTime.UtcNow.AddDays(-90);
  var recurring=db.Faults.AsNoTracking()
   .Where(x=>x.IsActive&&x.OccurredAt>=since)
   .GroupBy(x=>new{x.VehicleId,x.FaultCategoryId})
   .Where(group=>group.Count()>=2)
   .Select(group=>new
   {
    group.Key.VehicleId,group.Key.FaultCategoryId,
    FaultCount=group.LongCount(),FirstFaultAt=group.Min(x=>x.OccurredAt),LastFaultAt=group.Max(x=>x.OccurredAt)
   });
  var query=from repeat in recurring
            join vehicle in db.Vehicles.AsNoTracking() on repeat.VehicleId equals vehicle.Id
            join category in db.FaultCategories.AsNoTracking() on repeat.FaultCategoryId equals category.Id
            select new{repeat,vehicle,category};
  if(User.IsInRole("Garaj Yetkilisi"))query=query.Where(x=>x.vehicle.GarageId==User.GarageId());
  return Ok(await query.OrderByDescending(x=>x.repeat.FaultCount).ThenByDescending(x=>x.repeat.LastFaultAt).Select(x=>new
  {
   x.vehicle.Id,x.vehicle.DoorNumber,x.vehicle.Plate,Garage=x.vehicle.Garage.Name,
   Category=x.category.Name,x.repeat.FaultCount,x.repeat.FirstFaultAt,x.repeat.LastFaultAt,
   RecurrenceWindowDays=90
  }).ToListAsync());
 }

 // Garaj yetkilisi kendi garajındaki bütün araçların sağlık durumunu görür. Admin ve merkez
 // ekranında ise binlerce kusursuz kayıt yerine yalnızca geçmişinde arıza bulunan araçlar listelenir.
 [HttpGet("vehicle-health")]
 public async Task<IActionResult> VehicleHealth([FromQuery]int take=100)
 {
  var query=from health in db.VwVehicleHealthScores.AsNoTracking()
            join vehicle in db.Vehicles.AsNoTracking() on health.VehicleId equals vehicle.Id
            select new{health,vehicle};
  if(User.IsInRole("Garaj Yetkilisi"))
   query=query.Where(x=>x.health.GarageId==User.GarageId());
  else
   query=query.Where(x=>db.Faults.Any(fault=>fault.VehicleId==x.health.VehicleId));

  return Ok(await query.OrderBy(x=>x.health.HealthScore).ThenBy(x=>x.health.DoorNumber)
   .Take(Math.Clamp(take,1,5000)).Select(x=>new
  {
   x.health.VehicleId,x.health.DoorNumber,x.vehicle.Plate,Garage=x.vehicle.Garage.Name,
   Status=x.vehicle.VehicleStatus.Name,x.health.HealthScore,x.health.Faults90d,
   x.health.Faults30d,x.health.FailedInspections90d
  }).ToListAsync());
 }
}
