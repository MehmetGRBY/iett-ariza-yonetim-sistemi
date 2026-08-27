using IettFaultManagement.Api.Data;using IettFaultManagement.Api.Dtos;using IettFaultManagement.Api.Extensions;using IettFaultManagement.Api.Models.Database;using IettFaultManagement.Api.Services;using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using Microsoft.EntityFrameworkCore;
namespace IettFaultManagement.Api.Controllers;
[ApiController,Authorize(Roles="Admin,Merkez Yetkilisi,Garaj Yetkilisi"),Route("api/decision-support")]
/// <summary>
/// Çözüm bilgi bankası, araç kontrol kayıtları ve operasyonel olaylar için
/// karar destek endpoint'lerini barındırır.
/// </summary>
public sealed class DecisionSupportController(ApplicationDbContext db,AppNotificationService notifications):ControllerBase
{
 [HttpGet("solutions")]public async Task<IActionResult> Solutions([FromQuery]long? categoryId=null){var q=db.SolutionArticles.AsNoTracking().Where(x=>x.IsActive);if(categoryId.HasValue)q=q.Where(x=>x.FaultCategoryId==categoryId);return Ok(await q.OrderByDescending(x=>x.ApprovedAt??x.CreatedAt).ToListAsync());}
 [Authorize(Roles="Admin,Garaj Yetkilisi"),HttpPost("solutions")]public async Task<IActionResult> CreateSolution(CreateSolutionArticleRequest request){var item=new SolutionArticle{FaultCategoryId=request.FaultCategoryId!.Value,RootCauseId=request.RootCauseId,SourceRepairReportId=request.SourceRepairReportId,Title=request.Title.Trim(),Symptoms=request.Symptoms.Trim(),SolutionSteps=request.SolutionSteps.Trim(),SafetyNotes=request.SafetyNotes?.Trim(),EstimatedMinutes=request.EstimatedMinutes,ApprovalStatus=User.IsInRole("Admin")?"APPROVED":"DRAFT",CreatedByUserId=User.UserId(),ApprovedByUserId=User.IsInRole("Admin")?User.UserId():null,ApprovedAt=User.IsInRole("Admin")?DateTime.UtcNow:null,IsActive=true,CreatedAt=DateTime.UtcNow};db.SolutionArticles.Add(item);await db.SaveChangesAsync();return Created($"/api/decision-support/solutions/{item.Id}",new{item.Id,item.ApprovalStatus});}
 [Authorize(Roles="Admin"),HttpPut("solutions/{id:long}/approve")]public async Task<IActionResult> Approve(long id){var item=await db.SolutionArticles.FindAsync(id);if(item is null)return NotFound();item.ApprovalStatus="APPROVED";item.ApprovedByUserId=User.UserId();item.ApprovedAt=DateTime.UtcNow;await db.SaveChangesAsync();return NoContent();}
 [HttpGet("inspections")]public async Task<IActionResult> Inspections([FromQuery]long? vehicleId=null){var q=db.VehicleInspections.AsNoTracking();if(vehicleId.HasValue)q=q.Where(x=>x.VehicleId==vehicleId);if(User.IsInRole("Garaj Yetkilisi"))q=q.Where(x=>db.Vehicles.Any(v=>v.Id==x.VehicleId&&v.GarageId==User.GarageId()));var items=await(from inspection in q join vehicle in db.Vehicles.AsNoTracking() on inspection.VehicleId equals vehicle.Id join user in db.AppUsers.AsNoTracking() on inspection.InspectedByUserId equals user.Id into users from user in users.DefaultIfEmpty() join fault in db.Faults.AsNoTracking() on inspection.FaultId equals fault.Id into faults from fault in faults.DefaultIfEmpty() orderby inspection.CreatedAt descending select new{inspection.Id,inspection.VehicleId,Vehicle=new{vehicle.DoorNumber,vehicle.Plate,Garage=vehicle.Garage.Name,vehicle.CurrentMileage},inspection.FaultId,FaultNumber=fault==null?null:fault.FaultNumber,inspection.InspectionType,inspection.Result,inspection.Odometer,inspection.Notes,inspection.NextAction,inspection.InspectedAt,inspection.CreatedAt,Inspector=user==null?null:new{user.PersonnelNumber,FullName=user.FirstName+" "+user.LastName}}).Take(500).ToListAsync();return Ok(items);}
 /// <summary>
 /// Tamiri biten fakat yetkili tarafından kontrol sonucu girilmeyen araçları
 /// ayrı bir iş kuyruğu olarak döndürür. Garaj yetkilisi yalnızca kendi garajını görür.
 /// </summary>
 [HttpGet("inspection-queue")]
 public async Task<IActionResult> InspectionQueue(CancellationToken cancellationToken)
 {
  var query=db.Faults.AsNoTracking().Where(x=>x.IsActive&&x.FaultStatus.Code=="WAITING_INSPECTION");
  if(User.IsInRole("Garaj Yetkilisi")&&User.GarageId() is long garageId)
   query=query.Where(x=>x.GarageId==garageId);
  var items=await query.OrderBy(x=>x.OccurredAt).Select(x=>new
  {
   FaultId=x.Id,x.FaultNumber,x.OccurredAt,
   Vehicle=new{x.Vehicle.Id,x.Vehicle.DoorNumber,x.Vehicle.Plate,x.Vehicle.CurrentMileage},
   Garage=x.Garage.Name,Category=x.FaultCategory.Name,
   RepairResult=db.RepairReports.Where(r=>r.FaultAssignment.FaultId==x.Id&&r.IsActive&&r.IsSubmitted)
    .OrderByDescending(r=>r.SubmittedAt).Select(r=>r.Result).FirstOrDefault(),
   WaitingSince=x.FaultStatusHistories.Where(h=>h.NewStatus.Code=="WAITING_INSPECTION")
    .OrderByDescending(h=>h.ChangedAt).Select(h=>(DateTime?)h.ChangedAt).FirstOrDefault(),
   Attempt=db.FaultResponsePlans.Where(p=>p.FaultId==x.Id&&p.IsActive)
    .Select(p=>(int?)p.InspectionAttemptCount).FirstOrDefault()??0,
   MaxAttempts=db.FaultResponsePlans.Where(p=>p.FaultId==x.Id&&p.IsActive)
    .Select(p=>(int?)p.MaxInspectionAttempts).FirstOrDefault()??3
  }).ToListAsync(cancellationToken);
  return Ok(items);
 }
 /// <summary>Kontrol formundaki araç aramasını rolün garaj kapsamına göre sınırlar.</summary>
 [HttpGet("inspection-vehicles")]public async Task<IActionResult> InspectionVehicles([FromQuery]string? search=null){var q=db.Vehicles.AsNoTracking().Where(x=>x.IsActive);if(User.IsInRole("Garaj Yetkilisi"))q=q.Where(x=>x.GarageId==User.GarageId());if(!string.IsNullOrWhiteSpace(search)){var pattern=$"%{search.Trim()}%";q=q.Where(x=>EF.Functions.ILike(x.DoorNumber,pattern)||EF.Functions.ILike(x.Plate,pattern));}return Ok(await q.OrderBy(x=>x.Garage.Name).ThenBy(x=>x.DoorNumber).Take(50).Select(x=>new{x.Id,x.DoorNumber,x.Plate,x.CurrentMileage,Garage=x.Garage.Name}).ToListAsync());}
 [Authorize(Roles="Admin,Merkez Yetkilisi,Garaj Yetkilisi"),HttpPost("inspections")]
 public async Task<IActionResult> Inspect(CreateInspectionRequest request,CancellationToken cancellationToken)
 {
  var validTypes=new[]{"POST_REPAIR","TEST_DRIVE","RETURN_TO_SERVICE"};
  var validResults=new[]{"PENDING","PASSED","FAILED","CONDITIONAL"};
  var type=request.InspectionType.Trim().ToUpperInvariant();
  var result=request.Result.Trim().ToUpperInvariant();
  if(!validTypes.Contains(type)||!validResults.Contains(result))
   return BadRequest(new{message="Geçerli kontrol türü ve sonucu seçin."});

  var vehicle=await db.Vehicles.FindAsync([request.VehicleId!.Value],cancellationToken);
  if(vehicle is null)return NotFound();
  if(User.IsInRole("Garaj Yetkilisi")&&vehicle.GarageId!=User.GarageId())return Forbid();
  if(request.Odometer.HasValue&&request.Odometer<vehicle.CurrentMileage)
   return BadRequest(new{message=$"Kilometre {vehicle.CurrentMileage} değerinden küçük olamaz."});

  Fault? fault=null;
  if(request.FaultId.HasValue)
  {
   fault=await db.Faults.Include(x=>x.FaultStatus)
    .SingleOrDefaultAsync(x=>x.Id==request.FaultId&&x.VehicleId==vehicle.Id,cancellationToken);
   if(fault is null)return BadRequest(new{message="Arıza kaydı seçilen araca ait değil."});
   // Aynı kontrol formunun iki kez gönderilmesi yeni tamir denemeleri üretmemelidir.
   // Tamir sonrası kontrol yalnızca gerçekten kontrol bekleyen arızada kabul edilir.
   if(type=="POST_REPAIR"&&fault.FaultStatus.Code!="WAITING_INSPECTION")
    return Conflict(new{message=$"Bu arıza şu anda '{fault.FaultStatus.Name}' durumunda; yeni kontrol kaydı oluşturulamaz."});
   var hasReport=await db.RepairReports.AnyAsync(x=>x.FaultAssignment.FaultId==fault.Id&&x.IsActive&&x.IsSubmitted,cancellationToken);
   if(!hasReport)return BadRequest(new{message="Tamir sonrası kontrol için önce teknik rapor gönderilmelidir."});
  }

  var now=DateTime.UtcNow;
  var userId=User.UserId();
  var roleId=await db.AppUsers.Where(x=>x.Id==userId).Select(x=>x.RoleId).SingleAsync(cancellationToken);
  var item=new VehicleInspection{VehicleId=vehicle.Id,FaultId=request.FaultId,InspectionType=type,Result=result,
   Odometer=request.Odometer,Notes=request.Notes?.Trim(),InspectedByUserId=userId,InspectedAt=now,
   NextAction=request.NextAction?.Trim(),CreatedAt=now};
  db.VehicleInspections.Add(item);
  if(request.Odometer.HasValue)vehicle.CurrentMileage=Math.Max(vehicle.CurrentMileage,request.Odometer.Value);
  db.AuditLogs.Add(new AuditLog{UserId=userId,RoleId=roleId,Action="VEHICLE_INSPECTION_CREATED",
   EntityType="vehicle_inspections",EntityId=null,Description=fault is null
    ? $"{vehicle.DoorNumber} için araç kontrolü kaydedildi."
    : $"{fault.FaultNumber} için tamir sonrası kontrol sonucu: {result}.",CreatedAt=now});
  if(fault is not null)
  {
   // Tamir sonrası kontrol, sunum akışının karar kapısıdır. Başarılı
   // sonuç kaydı kapatmaya hazırlar; başarısız sonuç en fazla üç tamir denemesi başlatır.
   if(type=="POST_REPAIR")
   {
    var plan=await db.FaultResponsePlans.SingleOrDefaultAsync(x=>x.FaultId==fault.Id&&x.IsActive,cancellationToken);
    if(plan is not null)
    {
     plan.InspectionAttemptCount++;
     var oldStatusId=fault.FaultStatusId;
     string targetCode;
     string historyDescription;
     if(result is "PASSED" or "CONDITIONAL")
     {
      targetCode="RESOLVED";
      plan.ReadyToClose=true;
      // Başarılı kontrol sonrası kullanıcı sonucu ekranda görebilsin diye
      // arıza yapılandırılmış süre boyunca Çözüldü durumunda kalır, ardından worker kapatır.
      var closeSeconds=await ReadIntegerSettingAsync(db,"presentation_repair_seconds",10,cancellationToken);
      plan.AutomationEnabled=true;
      plan.AutomationStatus="READY_TO_CLOSE";
      plan.NextAutomationAt=now.AddSeconds(closeSeconds);
      historyDescription=$"Kontrol başarılı; arıza {closeSeconds} saniye sonra otomatik kapatılacak.";
     }
     else if(result=="FAILED"&&plan.InspectionAttemptCount>=plan.MaxInspectionAttempts)
     {
      // Üçüncü başarısız kontrolde araç hizmete döndürülmez.
      targetCode="CLOSED";
      plan.ReadyToClose=false;
      plan.AutomationEnabled=false;
      plan.AutomationStatus="COMPLETED";
      plan.AutomationCompletedAt=now;
      plan.NextAutomationAt=null;
      plan.LastAutomationError=null;
      fault.ClosedAt=now;
      var outOfService=await db.VehicleStatuses.SingleAsync(x=>x.Code=="OUT_OF_SERVICE",cancellationToken);
      var oldVehicleStatusId=vehicle.VehicleStatusId;
      vehicle.VehicleStatusId=outOfService.Id;
      if(oldVehicleStatusId!=outOfService.Id)
       db.VehicleStatusHistories.Add(new VehicleStatusHistory{VehicleId=vehicle.Id,OldStatusId=oldVehicleStatusId,
        NewStatusId=outOfService.Id,ChangedByUserId=userId,ChangedAt=now,FaultId=fault.Id,
        Description="Üçüncü başarısız tamir sonrası kontrol nedeniyle araç servis dışı bırakıldı."});
      var reserveGarage=await db.Garages.SingleOrDefaultAsync(x=>x.Code=="ARV",cancellationToken);
      if(reserveGarage is not null&&vehicle.GarageId!=reserveGarage.Id)
      {
       var oldGarageId=vehicle.GarageId;
       vehicle.GarageId=reserveGarage.Id;
       db.VehicleGarageHistories.Add(new VehicleGarageHistory{VehicleId=vehicle.Id,OldGarageId=oldGarageId,
        NewGarageId=reserveGarage.Id,ChangedByUserId=userId,ChangedAt=now,
        Description="Üç başarısız tamir sonrası kontrol nedeniyle ARV yedek garajına sevk edildi."});
      }
      historyDescription=$"Araç {plan.InspectionAttemptCount}. kontrolü de geçemedi; arıza kapatıldı ve araç servis dışı bırakıldı.";
      db.AuditLogs.Add(new AuditLog{UserId=userId,RoleId=roleId,Action="FAULT_CLOSED_AFTER_INSPECTION_FAILURE",
       EntityType="faults",EntityId=fault.Id,Description=historyDescription,CreatedAt=now});
     }
     else
     {
      plan.ReadyToClose=false;
      // Önceki ekip boşaltıldıysa aynı garajdan adil sıradaki ekip yeniden atanır.
      var hasActiveAssignment=await db.FaultAssignments.AnyAsync(x=>x.FaultId==fault.Id&&x.IsActive,cancellationToken);
      if(!hasActiveAssignment)
      {
       var retryTeam=await db.TechnicianTeams.Where(x=>x.GarageId==fault.GarageId&&x.IsActive&&x.IsAvailable&&
         !db.FaultAssignments.Any(a=>a.TeamId==x.Id&&a.IsActive))
        .OrderBy(x=>x.LastAssignedAt==null?0:1)
        .ThenBy(x=>x.LastAssignedAt)
        .ThenBy(x=>x.Id)
        .FirstOrDefaultAsync(cancellationToken);
       if(retryTeam is not null)
       {
        db.FaultAssignments.Add(new FaultAssignment{FaultId=fault.Id,TeamId=retryTeam.Id,AssignedByUserId=userId,
         IsAutomatic=true,AssignedAt=now,StartedAt=now,IsActive=true});
        retryTeam.IsAvailable=false;retryTeam.LastAssignedAt=now;
        var retryMembers=await db.TeamMembers
         .Where(x=>x.TeamId==retryTeam.Id&&x.IsActive)
         .ToListAsync(cancellationToken);
        foreach(var member in retryMembers)member.WorkStatus="ON_DUTY";
        hasActiveAssignment=true;
       }
      }

      if(hasActiveAssignment)
      {
       targetCode="REPAIR_IN_PROGRESS";
       // Başarısız kontrolden sonra ekip atanır fakat tamir sonucu yine kullanıcıya bırakılır.
       plan.AutomationEnabled=false;
       plan.AutomationStatus="MANUAL_REPAIR_REQUIRED";
       plan.RepairStartedAt=now;
       plan.NextAutomationAt=null;
       historyDescription=$"Kontrol başarısız ({plan.InspectionAttemptCount}/{plan.MaxInspectionAttempts}); araç yeni ekip atamasıyla yeniden tamire alındı.";
      }
      else
      {
       // Ekip yoksa tamir başlamış gibi gösterilmez; FIFO kuyruk worker'ı ilk boşalan ekibi atar.
       targetCode="WAITING_TEAM";
       plan.AutomationEnabled=false;
       plan.AutomationStatus="WAITING_TEAM";
       plan.NextAutomationAt=null;
       historyDescription=$"Kontrol başarısız ({plan.InspectionAttemptCount}/{plan.MaxInspectionAttempts}); bütün ekipler meşgul olduğu için araç sıraya alındı.";
      }
     }
     var target=await db.FaultStatuses.SingleAsync(x=>x.Code==targetCode,cancellationToken);
     fault.FaultStatusId=target.Id;
     db.FaultStatusHistories.Add(new FaultStatusHistory{FaultId=fault.Id,OldStatusId=oldStatusId,NewStatusId=target.Id,
      ChangedByUserId=userId,ChangedByRoleId=roleId,Description=historyDescription,IsSystemAction=true,ChangedAt=now});
    }
   }
   // Kontrol sonucu merkeze anında bildirilir; bildirim faultId içerdiği için tıklanınca detay açılır.
   await notifications.NotifyCentralAsync(fault.Id,"Araç kontrolü tamamlandı",
    $"{fault.FaultNumber} için araç kontrol sonucu {result}. Merkez kararı bekleniyor.",
    "VEHICLE_INSPECTION_COMPLETED",now,cancellationToken);
  }
  await db.SaveChangesAsync(cancellationToken);
  return Created($"/api/decision-support/inspections/{item.Id}",new{item.Id});
 }

 private static async Task<int> ReadIntegerSettingAsync(ApplicationDbContext db,string key,int fallback,CancellationToken ct)
 {
  var json=await db.SystemSettings.AsNoTracking().Where(x=>x.SettingKey==key&&x.IsActive)
   .Select(x=>x.SettingValue).SingleOrDefaultAsync(ct);
  return int.TryParse(json,out var value)?Math.Clamp(value,1,3600):fallback;
 }
 [HttpGet("operational-events")]
 public async Task<IActionResult> Events()=>Ok(await db.OperationalEvents.AsNoTracking()
  .OrderByDescending(x=>x.StartsAt).Take(500)
  .Select(x=>new{x.Id,x.EventNumber,x.EventType,x.Title,x.Description,x.GarageId,
   Garage=x.GarageId.HasValue?db.Garages.Where(g=>g.Id==x.GarageId).Select(g=>g.Name).FirstOrDefault():null,
   x.RouteId,Route=x.RouteId.HasValue?db.Routes.Where(r=>r.Id==x.RouteId).Select(r=>r.Code+" · "+r.Name).FirstOrDefault():null,
   x.StartsAt,x.EndsAt,x.Status,x.CreatedByUserId,x.CreatedAt}).ToListAsync());
 [Authorize(Roles="Admin,Merkez Yetkilisi"),HttpPost("operational-events")]
 public async Task<IActionResult> CreateEvent(CreateOperationalEventRequest request)
 {
  var now=DateTime.UtcNow;var startsAt=request.StartsAt.ToUniversalTime();var endsAt=request.EndsAt?.ToUniversalTime();
  if(endsAt.HasValue&&endsAt.Value<startsAt)return BadRequest(new{message="Bitiş zamanı başlangıç zamanından önce olamaz."});
  var item=new OperationalEvent{EventNumber=$"OLY-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",EventType=request.EventType,Title=request.Title.Trim(),Description=request.Description.Trim(),GarageId=request.GarageId,RouteId=request.RouteId,StartsAt=startsAt,EndsAt=endsAt,Status=endsAt.HasValue&&endsAt<=now?"RESOLVED":"OPEN",CreatedByUserId=User.UserId(),CreatedAt=now};
  db.OperationalEvents.Add(item);
  var scope=request.GarageId.HasValue
   ? await db.Garages.Where(x=>x.Id==request.GarageId).Select(x=>x.Name).SingleOrDefaultAsync()??"Seçilen garaj"
   : "Tüm garajlar";
  await notifications.NotifyOperationalEventAsync(request.GarageId,$"Operasyon olayı: {item.Title}",
   $"{item.EventNumber} numaralı olay oluşturuldu. Kapsam: {scope}. {item.Description}",
   "OPERATIONAL_EVENT_CREATED",now);
  await db.SaveChangesAsync();return Created($"/api/decision-support/operational-events/{item.Id}",new{item.Id,item.EventNumber});
 }
 /// <summary>Operasyon olayının bilgilerini, gerçek bitiş zamanını ve açık/kapalı durumunu günceller.</summary>
 [Authorize(Roles="Admin,Merkez Yetkilisi"),HttpPut("operational-events/{id:long}")]
 public async Task<IActionResult> UpdateEvent(long id,UpdateOperationalEventRequest request)
 {
  var item=await db.OperationalEvents.SingleOrDefaultAsync(x=>x.Id==id);
  if(item is null)return NotFound();
  var status=request.Status.Trim().ToUpperInvariant();
  if(status is not ("OPEN" or "CLOSED" or "RESOLVED"))return BadRequest(new{message="Durum Açık veya Kapalı olmalıdır."});
  // Formdaki CLOSED kullanıcı terimidir; PostgreSQL kısıtındaki geçerli kapanış koduna çevrilir.
  if(status=="CLOSED")status="RESOLVED";
  var startsAt=request.StartsAt.ToUniversalTime();
  var endsAt=request.EndsAt?.ToUniversalTime();
  if(endsAt.HasValue&&endsAt.Value<startsAt)return BadRequest(new{message="Bitiş zamanı başlangıç zamanından önce olamaz."});
  if(status=="RESOLVED"&&!endsAt.HasValue)return BadRequest(new{message="Kapatılan olay için bitiş zamanı girilmelidir."});

  var oldValues=new{item.EventType,item.Title,item.Description,item.GarageId,item.RouteId,item.StartsAt,item.EndsAt,item.Status};
  item.EventType=request.EventType.Trim().ToUpperInvariant();item.Title=request.Title.Trim();item.Description=request.Description.Trim();
  item.GarageId=request.GarageId;item.RouteId=request.RouteId;item.StartsAt=startsAt;item.EndsAt=endsAt;
  item.Status=endsAt.HasValue&&endsAt<=DateTime.UtcNow?"RESOLVED":status;
  db.AuditLogs.Add(new AuditLog{UserId=User.UserId(),Action="OPERATIONAL_EVENT_UPDATED",EntityType="operational_events",EntityId=item.Id,
   OldValues=System.Text.Json.JsonSerializer.Serialize(oldValues),
   NewValues=System.Text.Json.JsonSerializer.Serialize(new{item.EventType,item.Title,item.Description,item.GarageId,item.RouteId,item.StartsAt,item.EndsAt,item.Status}),
   Description=$"{item.EventNumber} numaralı operasyon olayı güncellendi.",CreatedAt=DateTime.UtcNow});
  await db.SaveChangesAsync();return NoContent();
 }
}
