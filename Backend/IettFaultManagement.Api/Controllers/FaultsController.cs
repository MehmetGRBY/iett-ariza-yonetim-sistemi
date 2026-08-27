using IettFaultManagement.Api.Data;using IettFaultManagement.Api.Dtos;using IettFaultManagement.Api.Extensions;using IettFaultManagement.Api.Models.Database;using IettFaultManagement.Api.Services;using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using Microsoft.EntityFrameworkCore;
namespace IettFaultManagement.Api.Controllers;
[ApiController,Authorize(Roles="Admin,Merkez Yetkilisi,Garaj Yetkilisi"),Route("api/faults")]
/// <summary>
/// Arıza yönetiminin ana REST arayüzüdür: kayıt açma, detay/listeleme, durum geçişi
/// ve garaj teknik raporu işlemlerini koordine eder. Kaynak kararını policy ve servis katmanlarına devreder.
/// </summary>
public sealed class FaultsController(ApplicationDbContext db,FaultResourceAssignmentService resourceService,FaultInterventionPolicy interventionPolicy,FaultLifecycleService lifecycleService,AppNotificationService notifications):ControllerBase
{
 private IQueryable<Fault> Scoped(){var q=db.Faults.AsQueryable();return User.IsInRole("Garaj Yetkilisi")&&User.GarageId() is long g?q.Where(x=>x.GarageId==g):q;}
 [HttpGet]public async Task<IActionResult> Get([FromQuery]int page=1,[FromQuery]int pageSize=100,[FromQuery]long? statusId=null,[FromQuery]string? search=null){page=Math.Max(1,page);pageSize=Math.Clamp(pageSize,1,100);var q=Scoped().AsNoTracking();if(statusId.HasValue)q=q.Where(x=>x.FaultStatusId==statusId);if(!string.IsNullOrWhiteSpace(search)){var p=$"%{search.Trim()}%";q=q.Where(x=>EF.Functions.ILike(x.FaultNumber,p)||EF.Functions.ILike(x.Vehicle.DoorNumber,p)||EF.Functions.ILike(x.Description,p));}var count=await q.CountAsync();var items=await q.OrderByDescending(x=>x.OccurredAt).Skip((page-1)*pageSize).Take(pageSize).Select(x=>new{x.Id,x.FaultNumber,Vehicle=x.Vehicle.DoorNumber,x.Vehicle.Plate,Garage=x.Garage.Name,Driver=x.Driver==null?null:x.Driver.FirstName+" "+x.Driver.LastName,Category=x.FaultCategory.Name,Status=x.FaultStatus.Name,StatusCode=x.FaultStatus.Code,Team=x.FaultAssignments.OrderByDescending(a=>a.IsActive).ThenByDescending(a=>a.AssignedAt).Select(a=>a.Team.Name).FirstOrDefault(),x.OccurredAt,x.ClosedAt,x.IsActive,x.ResponseDueAt,x.ResolutionDueAt}).ToListAsync();return Ok(new{items,page,pageSize,totalCount=count,totalPages=(int)Math.Ceiling(count/(double)pageSize)});}
 // Detay verisi tek bir karmaşık sorguya sıkıştırılmaz. Ayrı sorgular EF Core çevirisini güvenilir,
 // kodu okunabilir ve ileride yeni detay bölümleri eklemeyi kolay hâle getirir.
 [HttpGet("{id:long}")]
 public async Task<IActionResult> Details(long id)
 {
  var fault=await Scoped().AsNoTracking().Where(x=>x.Id==id).Select(x=>new
  {
   x.Id,x.FaultNumber,
   Vehicle=new{x.Vehicle.Id,x.Vehicle.DoorNumber,x.Vehicle.Plate,x.Vehicle.Brand,x.Vehicle.Model},
   Garage=x.Garage.Name,
   Driver=x.Driver==null?null:new{x.Driver.Id,x.Driver.PersonnelNumber,FullName=x.Driver.FirstName+" "+x.Driver.LastName},
   Category=x.FaultCategory.Name,Status=x.FaultStatus.Name,StatusCode=x.FaultStatus.Code,x.Description,x.MileageAtFailure,
   x.LocationDescription,x.OccurredAt,x.CreatedAt,x.ClosedAt,x.ResponseDueAt,x.ResolutionDueAt,
   Team=x.FaultAssignments.OrderByDescending(a=>a.IsActive).ThenByDescending(a=>a.AssignedAt)
    .Select(a=>a.Team.Name).FirstOrDefault()
  }).SingleOrDefaultAsync();

  if(fault is null)return NotFound();

  var responsePlan=await db.FaultResponsePlans.AsNoTracking()
   .Where(x=>x.FaultId==id&&x.IsActive)
   .Select(x=>new{x.MobilityStatus,x.CanCompleteCurrentTrip,x.CanContinueRemainingTasks,
    x.OnSiteRepairPossible,x.TowRequired,x.ServiceVehicleRequired,x.ReplacementVehicleRequired,
    x.DriverCanContinue,x.AssessmentNote,x.AutomationStatus,x.PlannedRepairMinutes,x.NextAutomationAt})
   .FirstOrDefaultAsync();

  var resources=await db.FaultResourceAssignments.AsNoTracking().Where(x=>x.FaultId==id)
   .OrderBy(x=>x.AssignedAt)
   .Select(x=>new{x.Id,x.ResourceType,x.VehicleId,x.DriverId,x.TechnicianTeamId,x.Status,x.AssignedAt,x.CompletedAt})
   .ToListAsync();

  var history=await db.FaultStatusHistories.AsNoTracking().Where(x=>x.FaultId==id)
   .OrderByDescending(x=>x.ChangedAt)
   .Select(x=>new{Status=x.NewStatus.Name,StatusCode=x.NewStatus.Code,x.Description,x.ChangedAt,
    User=x.ChangedByUser.FirstName+" "+x.ChangedByUser.LastName})
   .ToListAsync();

  var reports=await db.RepairReports.AsNoTracking()
   .Where(x=>x.FaultAssignment.FaultId==id&&x.IsActive)
   .OrderByDescending(x=>x.SubmittedAt)
   .Select(x=>new{x.Id,x.Result,x.Description,x.StartedAt,x.CompletedAt,x.SubmittedAt,
    x.RootCauseId,x.SolutionSummary,x.RecurrencePrevention,x.RequiresFollowUp})
   .ToListAsync();

  // Arızaya yüklenen aktif fotoğraf ve belgeler detay ekranında listelenir.
  // Gerçek dosya yolu istemciye verilmez; indirme işlemi yetki denetimli endpoint üzerinden yapılır.
  var attachments=await db.FaultAttachments.AsNoTracking()
   .Where(x=>x.FaultId==id&&x.IsActive)
   .OrderByDescending(x=>x.UploadedAt)
   .Select(x=>new{x.Id,x.OriginalFileName,x.ContentType,x.FileSize,x.UploadedAt})
   .ToListAsync();

  // Merkez yetkilisi kapanış kararı verirken bu arızaya ait son kontrol
  // sonucunu aynı detay ekranında görebilir.
  var inspections=await db.VehicleInspections.AsNoTracking()
   .Where(x=>x.FaultId==id)
   .OrderByDescending(x=>x.InspectedAt??x.CreatedAt)
   .Select(x=>new{x.Id,x.InspectionType,x.Result,x.Odometer,x.Notes,x.NextAction,x.InspectedAt})
   .ToListAsync();

  var allowedStatusCodes=lifecycleService.GetAllowedTargetCodes(fault.StatusCode);

  return Ok(new{fault.Id,fault.FaultNumber,fault.Vehicle,fault.Garage,fault.Driver,fault.Category,
   fault.Status,fault.StatusCode,AllowedStatusCodes=allowedStatusCodes,fault.Description,fault.MileageAtFailure,fault.LocationDescription,fault.OccurredAt,
   fault.CreatedAt,fault.ClosedAt,fault.ResponseDueAt,fault.ResolutionDueAt,fault.Team,
   ResponsePlan=responsePlan,Resources=resources,History=history,Reports=reports,Attachments=attachments,Inspections=inspections});
 }

 // Merkez personelinin formda yalnızca o anda görevi bulunan araçları hızlı seçmesini sağlar.
 [Authorize(Roles="Admin,Merkez Yetkilisi"),HttpGet("active-task-vehicles")]
 public async Task<IActionResult> ActiveTaskVehicles()
 {
  var now=DateTime.UtcNow;
  var items=await db.TaskAssignments.AsNoTracking()
   .Where(x=>x.IsActive&&x.ServiceTask.IsActive&&x.ServiceTask.PlannedDepartureAt<=now&&x.ServiceTask.PlannedArrivalAt>=now)
   .OrderBy(x=>x.Vehicle.DoorNumber)
   .Select(x=>new{x.Vehicle.Id,x.Vehicle.DoorNumber,x.Vehicle.Plate,x.Vehicle.Brand,x.Vehicle.Model,
    x.Vehicle.CurrentMileage,x.Vehicle.GarageId,Garage=x.Vehicle.Garage.Name,
    Driver=new{x.Driver.Id,x.Driver.PersonnelNumber,FullName=x.Driver.FirstName+" "+x.Driver.LastName},
    Task=new{x.ServiceTask.Id,x.ServiceTask.TaskNumber,x.ServiceTask.PlannedDepartureAt,x.ServiceTask.PlannedArrivalAt}})
   .ToListAsync();

  return Ok(items);
 }

 // Kapı numarasıyla araç bilgisi, o anki görev sürücüsü ve gerekirse seçilebilecek garaj sürücüleri döndürülür.
 [Authorize(Roles="Admin,Merkez Yetkilisi"),HttpGet("vehicle-context/{doorNumber}")]
 public async Task<IActionResult> VehicleContext(string doorNumber)
 {
  var vehicle=await db.Vehicles.AsNoTracking()
   .Where(x=>x.IsActive&&x.DoorNumber.ToUpper()==doorNumber.Trim().ToUpper())
   .Select(x=>new{x.Id,x.DoorNumber,x.Plate,x.Brand,x.Model,x.CurrentMileage,x.GarageId,Garage=x.Garage.Name,Status=x.VehicleStatus.Name})
   .SingleOrDefaultAsync();
  if(vehicle is null)return NotFound(new{message="Aktif araç bulunamadı."});

  var now=DateTime.UtcNow;
  var activeAssignment=await db.TaskAssignments.AsNoTracking()
   .Where(x=>x.IsActive&&x.VehicleId==vehicle.Id&&x.ServiceTask.IsActive&&x.ServiceTask.PlannedDepartureAt<=now&&x.ServiceTask.PlannedArrivalAt>=now)
   .Select(x=>new{x.Driver.Id,x.Driver.PersonnelNumber,FullName=x.Driver.FirstName+" "+x.Driver.LastName,
    TaskNumber=x.ServiceTask.TaskNumber})
   .FirstOrDefaultAsync();

  var availableDrivers=activeAssignment is null
   ?await db.Drivers.AsNoTracking().Where(x=>x.IsActive&&x.GarageId==vehicle.GarageId&&
      !db.TaskAssignments.Any(a=>a.IsActive&&a.DriverId==x.Id&&a.ServiceTask.IsActive&&a.ServiceTask.PlannedDepartureAt<=now&&a.ServiceTask.PlannedArrivalAt>=now))
     .OrderBy(x=>x.PersonnelNumber)
     .Select(x=>new{x.Id,x.PersonnelNumber,FullName=x.FirstName+" "+x.LastName})
     .ToListAsync()
   :[];

 return Ok(new{Vehicle=vehicle,ActiveAssignment=activeAssignment,AvailableDrivers=availableDrivers});
 }
 // Ön değerlendirme sonrası açılan kaynak adımı için aynı garajdaki
 // gerçekten müsait çekici, hizmet ve yolcu araçlarını ayrı listeler hâlinde döndürür.
 [Authorize(Roles="Admin,Merkez Yetkilisi"),HttpGet("resource-candidates")]
 public async Task<IActionResult> ResourceCandidates(long vehicleId)
 {
  var source=await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==vehicleId&&x.IsActive);
  if(source is null)return NotFound(new{message="Araç bulunamadı."});
  var candidates=db.Vehicles.AsNoTracking().Where(x=>x.IsActive&&x.GarageId==source.GarageId&&x.Id!=source.Id&&
   x.VehicleStatus.Code=="AVAILABLE"&&!db.FaultResourceAssignments.Any(r=>r.VehicleId==x.Id&&r.IsActive));
  var projection=candidates.OrderBy(x=>x.DoorNumber).Select(x=>new{x.Id,x.DoorNumber,x.Plate,Type=x.VehicleType.Name});
  var towTrucks=await projection.Where(x=>EF.Functions.ILike(x.Type,"%Çekici%")).ToListAsync();
  var serviceVehicles=await projection.Where(x=>EF.Functions.ILike(x.Type,"%Hizmet%")).ToListAsync();
  var replacementVehicles=await projection.Where(x=>EF.Functions.ILike(x.Type,"%Otobüs%")||EF.Functions.ILike(x.Type,"%Metrobüs%")).ToListAsync();
  var now=DateTime.UtcNow;
  var reserveDrivers=await db.Drivers.AsNoTracking().Where(x=>x.IsActive&&x.GarageId==source.GarageId&&
    x.DriverType=="RESERVE"&&x.AvailabilityStatus=="AVAILABLE"&&
    !db.FaultResourceAssignments.Any(r=>r.DriverId==x.Id&&r.IsActive)&&
    !db.TaskAssignments.Any(a=>a.DriverId==x.Id&&a.IsActive&&a.ServiceTask.PlannedDepartureAt<=now&&a.ServiceTask.PlannedArrivalAt>=now)&&
    !db.PersonnelIncidents.Any(i=>i.DriverId==x.Id&&i.IsActive&&i.Status!="CANCELLED"&&(i.ReportStatus=="PENDING"||i.ExpectedReturnAt>now)))
   .OrderBy(x=>x.PersonnelNumber).Select(x=>new{x.Id,x.PersonnelNumber,FullName=x.FirstName+" "+x.LastName}).ToListAsync();
  return Ok(new{TowTrucks=towTrucks,ServiceVehicles=serviceVehicles,ReplacementVehicles=replacementVehicles,ReserveDrivers=reserveDrivers});
 }
 // Arıza kategorisindeki geçmiş işlerin ortalama süresi ve başarı sayısına göre
 // uygun ekipleri sıralar. Son kararı kullanıcı verdiği için endpoint yalnızca öneri döndürür.
 [Authorize(Roles="Admin,Merkez Yetkilisi"),HttpGet("team-recommendations")]
 public async Task<IActionResult> TeamRecommendations(long garageId,long categoryId)
 {
  var teams=await db.TechnicianTeams.AsNoTracking()
   .Where(x=>x.GarageId==garageId&&x.IsActive&&x.IsAvailable&&
    !db.FaultAssignments.Any(a=>a.TeamId==x.Id&&a.IsActive&&a.Fault.IsActive&&a.Fault.ClosedAt==null))
   .Select(x=>new{x.Id,x.Name,x.LastAssignedAt}).ToListAsync();
  // PostgreSQL interval hesabını sorgu sağlayıcısına bağımlı bırakmamak için
  // yalnızca gerekli tarihler alınır ve küçük performans kümesi bellekte özetlenir.
  var teamIds=teams.Select(x=>x.Id).ToArray();
  var reportRows=await db.RepairReports.AsNoTracking()
   .Where(r=>teamIds.Contains(r.FaultAssignment.TeamId)&&r.FaultAssignment.Fault.FaultCategoryId==categoryId&&r.IsSubmitted)
   .Select(r=>new{r.FaultAssignment.TeamId,r.Result,r.StartedAt,r.CompletedAt}).ToListAsync();
  var scored=teams.Select(team=>
  {
   var reports=reportRows.Where(r=>r.TeamId==team.Id).ToList();
   return new{team.Id,team.Name,team.LastAssignedAt,CompletedCount=reports.Count,
    SuccessfulCount=reports.Count(r=>r.Result!="UNRESOLVED"),
    AverageMinutes=reports.Count==0?(double?)null:reports.Average(r=>(r.CompletedAt-r.StartedAt).TotalMinutes)};
  });
  var ordered=scored.OrderByDescending(x=>x.CompletedCount>0)
   .ThenByDescending(x=>x.CompletedCount==0?0:(double)x.SuccessfulCount/x.CompletedCount)
   .ThenBy(x=>x.AverageMinutes??double.MaxValue).ThenBy(x=>x.LastAssignedAt).ToList();
  return Ok(ordered.Select((x,index)=>new{x.Id,x.Name,x.CompletedCount,x.SuccessfulCount,x.AverageMinutes,IsRecommended=index==0}));
 }

 [Authorize(Roles="Admin,Merkez Yetkilisi"),HttpPost]
 public async Task<IActionResult> Create(CreateFaultRequest request,CancellationToken cancellationToken)
 {
  var now=DateTime.UtcNow;
  var vehicle=await db.Vehicles.SingleOrDefaultAsync(x=>x.IsActive&&x.DoorNumber.ToUpper()==request.DoorNumber.Trim().ToUpper(),cancellationToken);
  if(vehicle is null)return BadRequest(new{message="Aktif araç bulunamadı."});
  // Bir araçta aynı anda yalnızca bir açık arıza bulunabilir. Açıklamanın farklı
  // olması ikinci kayıt açılmasına izin vermez; yeni bulgu mevcut kayda eklenmelidir.
  if(await db.Faults.AnyAsync(x=>x.VehicleId==vehicle.Id&&x.IsActive&&x.ClosedAt==null,cancellationToken))
   return Conflict(new{message="Bu araç için zaten açık bir arıza bulunuyor. Yeni kayıt açmadan mevcut arızayı sonuçlandırın."});

  var active=await db.TaskAssignments.Include(x=>x.ServiceTask)
   .FirstOrDefaultAsync(x=>x.IsActive&&x.VehicleId==vehicle.Id&&x.ServiceTask.PlannedDepartureAt<=now&&x.ServiceTask.PlannedArrivalAt>=now,cancellationToken);
  var contexts=new[]{"ACTIVE_TASK","TEST_DRIVE","GARAGE_CHECK","TRANSFER","PRE_SERVICE_CHECK","OTHER"};
  var operationContext=active is not null?"ACTIVE_TASK":request.OperationContext.Trim().ToUpperInvariant();
  if(!contexts.Contains(operationContext))return BadRequest(new{message="Geçerli arıza oluşma durumu seçin."});

  // Aktif görevde sürücü görevden gelir. Test sürüşü/transferde seçim zorunlu,
  // garaj ve servis öncesi kontrolde ise arıza sürücüsüz tespit edilebilir.
  var driverId=active?.DriverId??request.DriverId;
  var driverRequired=operationContext is "ACTIVE_TASK" or "TEST_DRIVE" or "TRANSFER";
  if(driverRequired&&!driverId.HasValue)return BadRequest(new{message="Bu işlem türünde aracı kullanan sürücü seçilmelidir."});
  Driver? driver=null;
  if(driverId.HasValue)
  {
   driver=await db.Drivers.FindAsync([driverId.Value],cancellationToken);
   if(driver is null||!driver.IsActive||driver.GarageId!=vehicle.GarageId)
    return BadRequest(new{message="Geçerli bir garaj sürücüsü seçin."});
  }

  var category=await db.FaultCategories.SingleOrDefaultAsync(x=>x.Id==request.FaultCategoryId&&x.IsActive&&x.ParentCategoryId!=null,cancellationToken);
  if(category is null)return BadRequest(new{message="Geçerli bir alt kategori seçin."});
  if(request.MileageAtFailure<vehicle.CurrentMileage)return BadRequest(new{message=$"Kilometre {vehicle.CurrentMileage} değerinden küçük olamaz."});
  var decision=interventionPolicy.Decide(request.MobilityStatus,request.OnSiteRepairDecision,
   request.CanContinueRemainingTasks,request.CanCompleteCurrentTrip);
  if(request.MobilityStatus.Equals("IMMOBILE",StringComparison.OrdinalIgnoreCase)&&
     (request.CanCompleteCurrentTrip||request.CanContinueRemainingTasks))
   return BadRequest(new{message="Hareket edemeyen araç mevcut seferini veya bugünün kalan görevlerini tamamlayamaz."});
  if(request.CanContinueRemainingTasks&&!request.CanCompleteCurrentTrip)
   return BadRequest(new{message="Bugünün kalan görevlerine devam edebilmesi için araç önce mevcut görevini tamamlayabilmelidir."});
  if(decision.TowRequired&&!request.TowTruckId.HasValue)return BadRequest(new{message="Bu karar için bir çekici seçmelisiniz."});
  if(decision.ServiceVehicleRequired&&!request.ServiceVehicleId.HasValue)return BadRequest(new{message="Bu karar için bir hizmet aracı seçmelisiniz."});
  if(decision.ReplacementVehicleRequired&&!request.ReplacementVehicleId.HasValue)return BadRequest(new{message="Seferlerin devamı için bir yedek araç seçmelisiniz."});
  if(decision.ReplacementVehicleRequired&&!request.ReplacementDriverId.HasValue)return BadRequest(new{message="Seferlerin devamı için yedek aracı kullanacak sürücüyü seçmelisiniz."});
  var assigned=await db.FaultStatuses.SingleAsync(x=>x.Code=="ASSIGNED_TO_TEAM",cancellationToken);
  var waitingTeam=await db.FaultStatuses.SingleAsync(x=>x.Code=="WAITING_TEAM",cancellationToken);

  var teamQuery=db.TechnicianTeams.Where(x=>x.GarageId==vehicle.GarageId&&x.IsActive&&x.IsAvailable&&
   !db.FaultAssignments.Any(a=>a.TeamId==x.Id&&a.IsActive&&a.Fault.ClosedAt==null));
  // Kullanıcı bir ekip seçtiyse yalnızca o ekip kullanılır. Seçim yapılmamışsa
  // en uzun süredir iş almayan müsait ekip seçilir.
  // Hiç ekip yoksa arıza FIFO kuyruğuna girer.
  var team=request.TechnicianTeamId.HasValue
   ?await teamQuery.SingleOrDefaultAsync(x=>x.Id==request.TechnicianTeamId.Value,cancellationToken)
   :await teamQuery.OrderBy(x=>x.LastAssignedAt==null?0:1).ThenBy(x=>x.LastAssignedAt).ThenBy(x=>x.Id)
    .FirstOrDefaultAsync(cancellationToken);
  if(request.TechnicianTeamId.HasValue&&team is null)return Conflict(new{message="Seçilen teknik ekip artık müsait değil."});

  var hasDispatchedResource=decision.TowRequired||decision.ServiceVehicleRequired||decision.ReplacementVehicleRequired;
  // Araç mevcut seferini tamamlayabiliyorsa saha/tamir akışı seferin planlanan
  // bitişinden önce başlamaz. Bu sürede kaynaklar ve teknik ekip beklemede kalır.
  var waitForCurrentTask=active is not null&&request.MobilityStatus.Equals("MOVABLE",StringComparison.OrdinalIgnoreCase)&&request.CanCompleteCurrentTrip;
  var waitForTodaysTasks=waitForCurrentTask&&decision.CanContinueRemainingTasks;
  var operationWaitUntil=active?.ServiceTask.PlannedArrivalAt;
  if(waitForTodaysTasks)
  {
   operationWaitUntil=await db.TaskAssignments.AsNoTracking()
    .Where(x=>x.IsActive&&x.VehicleId==vehicle.Id&&x.ServiceTask.IsActive&&
     x.ServiceTask.ServiceDate==active!.ServiceTask.ServiceDate)
    .MaxAsync(x=>(DateTime?)x.ServiceTask.PlannedArrivalAt,cancellationToken)
    ?? active!.ServiceTask.PlannedArrivalAt;
  }
  // Uygulamanın tek operasyon biçimi yarı otomatiktir: kaynak hareketleri
  // zamanlanır, tamir raporu ve kontrol kararları kullanıcı tarafından girilir.
  var departing=hasDispatchedResource
   ?await db.FaultStatuses.SingleAsync(x=>x.Code=="RESOURCES_DEPARTING",cancellationToken)
   :null;
  var waitingCurrentTask=waitForCurrentTask
   ?await db.FaultStatuses.SingleAsync(x=>x.Code==(waitForTodaysTasks?"WAITING_TODAYS_TASKS_END":"WAITING_CURRENT_TASK_END"),cancellationToken)
   :null;
  var dispatchSecondsJson=await db.SystemSettings.AsNoTracking().Where(x=>x.SettingKey=="presentation_dispatch_seconds"&&x.IsActive)
   .Select(x=>x.SettingValue).SingleOrDefaultAsync(cancellationToken);
  var dispatchSeconds=int.TryParse(dispatchSecondsJson,out var parsedDispatch)?Math.Clamp(parsedDispatch,1,3600):10;
  var maxAttemptsJson=await db.SystemSettings.AsNoTracking().Where(x=>x.SettingKey=="max_post_repair_inspection_attempts"&&x.IsActive)
   .Select(x=>x.SettingValue).SingleOrDefaultAsync(cancellationToken);
  var maxAttempts=int.TryParse(maxAttemptsJson,out var parsedMax)?Math.Clamp(parsedMax,1,10):3;

  await using var tx=await db.Database.BeginTransactionAsync(cancellationToken);
  var fault=new Fault{FaultNumber=$"ARZ-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",VehicleId=vehicle.Id,
   DriverId=driver?.Id,CreatedByUserId=User.UserId(),GarageId=vehicle.GarageId,FaultCategoryId=category.Id,
   FaultStatusId=waitingCurrentTask?.Id??departing?.Id??(team is null?waitingTeam.Id:assigned.Id),ServiceTaskId=active?.ServiceTaskId,Description=request.Description.Trim(),
   MileageAtFailure=request.MileageAtFailure,Latitude=0,Longitude=0,LocationDescription=request.LocationDescription.Trim(),
   OccurredAt=request.OccurredAt.Kind==DateTimeKind.Utc?request.OccurredAt:request.OccurredAt.ToUniversalTime(),CreatedAt=now,
   IsActive=true,ResponseDueAt=now.AddMinutes(category.ResponseSlaMinutes),ResolutionDueAt=now.AddMinutes(category.ResolutionSlaMinutes)};
  db.Faults.Add(fault);await db.SaveChangesAsync(cancellationToken);
  if(team is not null){db.FaultAssignments.Add(new FaultAssignment{FaultId=fault.Id,TeamId=team.Id,AssignedByUserId=User.UserId(),IsAutomatic=!request.TechnicianTeamId.HasValue,AssignedAt=now,IsActive=true});team.IsAvailable=false;team.LastAssignedAt=now;fault.FirstResponseAt=now;}
  db.FaultResponsePlans.Add(new FaultResponsePlan{FaultId=fault.Id,MobilityStatus=request.MobilityStatus.Trim().ToUpperInvariant(),CanCompleteCurrentTrip=request.MobilityStatus.Equals("MOVABLE",StringComparison.OrdinalIgnoreCase)&&request.CanCompleteCurrentTrip,CanContinueRemainingTasks=decision.CanContinueRemainingTasks,OnSiteRepairPossible=decision.OnSiteRepairPossible,TowRequired=decision.TowRequired,ServiceVehicleRequired=decision.ServiceVehicleRequired,ReplacementVehicleRequired=decision.ReplacementVehicleRequired,DriverCanContinue=false,AssessmentNote=$"Bağlam: {operationContext} | {request.AssessmentNote.Trim()}",AssessedByUserId=User.UserId(),AssessedAt=now,IsActive=true,OperationMode="MANUAL",AutomationEnabled=waitForCurrentTask||team is not null||hasDispatchedResource,AutomationStatus=waitForTodaysTasks?"WAITING_TODAYS_TASKS_END":waitForCurrentTask?"WAITING_CURRENT_TASK_END":hasDispatchedResource?"RESOURCE_DEPARTING":team is null?"WAITING_TEAM":"TEAM_ASSIGNED",NextAutomationAt=waitForCurrentTask?operationWaitUntil:team is not null||hasDispatchedResource?now.AddSeconds(dispatchSeconds):null,PlannedRepairMinutes=decision.OnSiteRepairPossible==true?category.OnsiteRepairMinutes:category.EstimatedRepairMinutes,PlannedRepairResult=category.AutoRepairResult,MaxInspectionAttempts=maxAttempts});
  db.FaultStatusHistories.Add(new FaultStatusHistory{FaultId=fault.Id,NewStatusId=fault.FaultStatusId,ChangedByUserId=User.UserId(),ChangedByRoleId=await db.AppUsers.Where(x=>x.Id==User.UserId()).Select(x=>x.RoleId).SingleAsync(cancellationToken),Description=waitForTodaysTasks?$"Araç bugünkü görevlerini {operationWaitUntil!.Value.ToLocalTime():dd.MM.yyyy HH:mm} saatine kadar tamamlayacak; garaja dönüş akışı son görevden sonra başlayacak.":waitForCurrentTask?$"Araç mevcut görevini {active!.ServiceTask.PlannedArrivalAt.ToLocalTime():dd.MM.yyyy HH:mm} saatine kadar tamamlayacak; garaja dönüş akışı görev sonunda başlayacak.":hasDispatchedResource?$"Manuel seçilen kaynaklar için {dispatchSeconds} saniyelik yarı otomatik saha akışı başlatıldı.":team is null?$"{operationContext} bağlamında arıza ekip bekleme sırasına alındı.":$"{operationContext} bağlamında seçilen {team.Name} ekibi tamire hazırlanıyor.",IsSystemAction=true,ChangedAt=now});
  vehicle.CurrentMileage=Math.Max(vehicle.CurrentMileage,request.MileageAtFailure);
  // Arıza açıldığı anda görevine kesintisiz devam edemeyen ana araç artık
  // "Göreve Hazır" görünmemelidir. Tamir başladığında worker bunu UNDER_REPAIR,
  // başarılı kapanışta ise yeniden AVAILABLE yapar.
  if(!decision.CanContinueRemainingTasks)
  {
   var faultyStatusId=await db.VehicleStatuses.Where(x=>x.Code=="FAULTY").Select(x=>x.Id).SingleAsync(cancellationToken);
   if(vehicle.VehicleStatusId!=faultyStatusId)
   {
    db.VehicleStatusHistories.Add(new VehicleStatusHistory{VehicleId=vehicle.Id,OldStatusId=vehicle.VehicleStatusId,
     NewStatusId=faultyStatusId,ChangedByUserId=User.UserId(),ChangedAt=now,FaultId=fault.Id,
     Description="Aktif arıza nedeniyle araç arızalı duruma alındı."});
    vehicle.VehicleStatusId=faultyStatusId;
   }
  }
  await db.SaveChangesAsync(cancellationToken);
  // Yedek araç her zaman kendi yedek sürücüsüyle gider; mevcut sürücü
  // hakkında ayrı bir karar kullanıcıdan istenmez.
  await resourceService.AssignRequiredAsync(fault,vehicle,driver,team?.Id,User.UserId(),decision.TowRequired,
   decision.ServiceVehicleRequired,decision.ReplacementVehicleRequired,false,now,
   request.TowTruckId,request.ServiceVehicleId,request.ReplacementVehicleId,request.ReplacementDriverId,
   request.CanCompleteCurrentTrip);
  await tx.CommitAsync(cancellationToken);
  return CreatedAtAction(nameof(Details),new{id=fault.Id},new{fault.Id,fault.FaultNumber});
 }
 [Authorize(Roles="Admin,Merkez Yetkilisi"),HttpPut("{id:long}/status")]
 public async Task<IActionResult> UpdateStatus(long id,UpdateFaultStatusRequest request,CancellationToken cancellationToken)
 {
  var fault=await db.Faults.Include(x=>x.FaultStatus).SingleOrDefaultAsync(x=>x.Id==id,cancellationToken);
  var status=await db.FaultStatuses.SingleOrDefaultAsync(x=>x.Id==request.StatusId&&x.IsActive,cancellationToken);
  if(fault is null||status is null)return NotFound();
  var userId=User.UserId();
  var roleId=await db.AppUsers.Where(x=>x.Id==userId).Select(x=>x.RoleId).SingleAsync(cancellationToken);
  await using var tx=await db.Database.BeginTransactionAsync(cancellationToken);
  try
  {
   await lifecycleService.ApplyAsync(fault,status,userId,roleId,request.Description,DateTime.UtcNow,cancellationToken);
   await db.SaveChangesAsync(cancellationToken);
   await tx.CommitAsync(cancellationToken);
   return NoContent();
  }
  catch(InvalidOperationException exception)
  {
   await tx.RollbackAsync(cancellationToken);
   return BadRequest(new{message=exception.Message});
  }
 }

 [Authorize(Roles="Admin,Garaj Yetkilisi"),HttpPost("{id:long}/reports")]
 public async Task<IActionResult> Report(long id,CreateRepairReportRequest request,CancellationToken cancellationToken)
 {
  var validResults=new[]{"REPAIRED","UNRESOLVED","TEMPORARY_REPAIR"};
  var result=request.Result.Trim().ToUpperInvariant();
  if(!validResults.Contains(result))return BadRequest(new{message="Geçerli teknik rapor sonucu seçin."});
  var fault=await Scoped().Include(x=>x.FaultStatus).Include(x=>x.FaultAssignments).ThenInclude(x=>x.Team)
   .SingleOrDefaultAsync(x=>x.Id==id,cancellationToken);
  if(fault is null)return NotFound();
  var activeAssignment=fault.FaultAssignments.Where(x=>x.IsActive).OrderByDescending(x=>x.AssignedAt).FirstOrDefault();
  if(fault.FaultStatus.Code!="REPAIR_IN_PROGRESS")
  {
   var alreadySubmitted=await db.RepairReports.AnyAsync(x=>x.FaultAssignment.FaultId==fault.Id&&x.IsActive&&x.IsSubmitted,cancellationToken);
   return alreadySubmitted
    ?Conflict(new{message="Bu tamir aşaması için teknik rapor zaten gönderildi."})
    :Conflict(new{message="Teknik rapor yalnızca Tamir Devam Ediyor aşamasında gönderilebilir."});
  }
  if(activeAssignment is null)return BadRequest(new{message="Arızaya ait aktif ekip ataması bulunamadı."});
  if(string.IsNullOrWhiteSpace(request.Description))return BadRequest(new{message="Teknik rapor açıklaması zorunludur."});
  if(request.StartedAt==default||request.CompletedAt==default)return BadRequest(new{message="Tamir başlangıç ve bitiş zamanları zorunludur."});
  if(request.CompletedAt<request.StartedAt)return BadRequest(new{message="Bitiş zamanı başlangıçtan önce olamaz."});
  if(request.CompletedAt.ToUniversalTime()>DateTime.UtcNow.AddMinutes(1))return BadRequest(new{message="Tamir bitiş zamanı gelecekte olamaz."});
  if(await db.RepairReports.AnyAsync(x=>x.FaultAssignmentId==activeAssignment.Id&&x.IsActive&&x.IsSubmitted,cancellationToken))
   return Conflict(new{message="Bu tamir denemesi için daha önce teknik rapor gönderildi."});

  var now=DateTime.UtcNow;
  var userId=User.UserId();
  var roleId=await db.AppUsers.Where(x=>x.Id==userId).Select(x=>x.RoleId).SingleAsync(cancellationToken);
  var plan=await db.FaultResponsePlans.SingleOrDefaultAsync(x=>x.FaultId==id&&x.IsActive,cancellationToken);
  var onSiteRepair=plan?.OnSiteRepairPossible==true;
  var nextStatusCode=onSiteRepair
   ?result=="UNRESOLVED"?"TOW_SELECTION_REQUIRED":"VEHICLE_RETURNING_TO_GARAGE"
   :"REPORT_SUBMITTED";
  var reportStatus=await db.FaultStatuses.SingleAsync(x=>x.Code==nextStatusCode,cancellationToken);
  await using var tx=await db.Database.BeginTransactionAsync(cancellationToken);
  var report=new RepairReport{FaultAssignmentId=activeAssignment.Id,CreatedByUserId=userId,Result=result,
   Description=request.Description.Trim(),StartedAt=request.StartedAt.ToUniversalTime(),CompletedAt=request.CompletedAt.ToUniversalTime(),
   SubmittedAt=now,IsSubmitted=true,IsActive=true,CreatedAt=now,RootCauseId=request.RootCauseId,
   SolutionSummary=request.SolutionSummary?.Trim(),RecurrencePrevention=request.RecurrencePrevention?.Trim(),RequiresFollowUp=request.RequiresFollowUp};
  db.RepairReports.Add(report);
  db.FaultStatusHistories.Add(new FaultStatusHistory{FaultId=fault.Id,OldStatusId=fault.FaultStatusId,
   NewStatusId=reportStatus.Id,ChangedByUserId=userId,ChangedByRoleId=roleId,
   Description=onSiteRepair
    ?result=="UNRESOLVED"?"Yerinde müdahale başarısız oldu; aracın garaja alınması için çekici seçimi gerekiyor.":"Yerinde tamir başarılı oldu; araç kontrol için garaja doğru yola çıktı."
    :"Garaj teknik raporu merkeze gönderildi.",IsSystemAction=false,ChangedAt=now});
  fault.FaultStatusId=reportStatus.Id;
  activeAssignment.StartedAt??=request.StartedAt.ToUniversalTime();
  activeAssignment.CompletedAt=request.CompletedAt.ToUniversalTime();
  activeAssignment.IsActive=false;
  activeAssignment.Team.IsAvailable=true;
  var members=await db.TeamMembers.Where(x=>x.TeamId==activeAssignment.TeamId&&x.IsActive).ToListAsync(cancellationToken);
  foreach(var member in members)member.WorkStatus="AVAILABLE";
  // Teknik rapor gönderildiğinde otomatik tamir senaryosu tamamlanır ve karar merkez yetkilisine geçer.
  // Veritabanındaki otomasyon durum kısıtı REPORT_SUBMITTED değerini kabul etmediğinden,
  // otomasyon için geçerli terminal değer kullanılır; arıza durumu zaten REPORT_SUBMITTED yapılmıştır.
  if(plan is not null)
  {
   if(onSiteRepair&&result!="UNRESOLVED")
   {
    var stepSeconds=await GetOperationStepSecondsAsync(cancellationToken);
    plan.AutomationEnabled=true;
    plan.AutomationStatus="ON_SITE_REPAIRED_RETURNING";
    plan.AutomationCompletedAt=null;
    plan.NextAutomationAt=now.AddSeconds(stepSeconds);
   }
   else if(onSiteRepair)
   {
    plan.AutomationEnabled=false;
    plan.AutomationStatus="AWAITING_TOW_SELECTION";
    plan.AutomationCompletedAt=null;
    plan.NextAutomationAt=null;
   }
   else
   {
    plan.AutomationEnabled=false;
    plan.AutomationStatus="COMPLETED";
    plan.AutomationCompletedAt=now;
    plan.NextAutomationAt=null;
   }
   plan.LastAutomationError=null;
  }
  db.AuditLogs.Add(new AuditLog{UserId=userId,RoleId=roleId,Action="REPAIR_REPORT_SUBMITTED",
   EntityType="faults",EntityId=fault.Id,Description="Teknik rapor merkeze gönderildi.",CreatedAt=now});
  // Garaj raporu gönderdiğinde admin ve merkez kullanıcıları ilgili arıza detayına yönlendirilir.
  await notifications.NotifyCentralAsync(fault.Id,"Teknik rapor gönderildi",
   $"{fault.FaultNumber} için garaj teknik raporu hazır. Merkez değerlendirmesi bekleniyor.",
   "REPAIR_REPORT_SUBMITTED",now,cancellationToken);
  await db.SaveChangesAsync(cancellationToken);
  await tx.CommitAsync(cancellationToken);
  return Created($"/api/faults/{id}",new{report.Id});
 }

 [Authorize(Roles="Admin,Merkez Yetkilisi"),HttpPost("{id:long}/dispatch-tow")]
 public async Task<IActionResult> DispatchTowAfterOnSiteFailure(long id,
  DispatchTowAfterOnSiteFailureRequest request,CancellationToken cancellationToken)
 {
  var fault=await Scoped().Include(x=>x.FaultStatus).Include(x=>x.Vehicle)
   .SingleOrDefaultAsync(x=>x.Id==id,cancellationToken);
  if(fault is null)return NotFound();
  if(fault.FaultStatus.Code!="TOW_SELECTION_REQUIRED")
   return Conflict(new{message="Çekici yalnızca başarısız yerinde müdahale sonrasında seçilebilir."});

  var plan=await db.FaultResponsePlans.SingleAsync(x=>x.FaultId==id&&x.IsActive,cancellationToken);
  var now=DateTime.UtcNow;
  var userId=User.UserId();
  var roleId=await db.AppUsers.Where(x=>x.Id==userId).Select(x=>x.RoleId).SingleAsync(cancellationToken);
  var departing=await db.FaultStatuses.SingleAsync(x=>x.Code=="RESOURCES_DEPARTING",cancellationToken);
  await using var tx=await db.Database.BeginTransactionAsync(cancellationToken);

  // Yerinde müdahaleyi taşıyan hizmet aracı görevini tamamlamıştır; çekici
  // operasyonu başlamadan önce araç ve sürücüsü yeniden müsait bırakılır.
  var serviceResources=await db.FaultResourceAssignments
   .Where(x=>x.FaultId==id&&x.IsActive&&x.ResourceType=="SERVICE_VEHICLE").ToListAsync(cancellationToken);
  var availableStatusId=await db.VehicleStatuses.Where(x=>x.Code=="AVAILABLE").Select(x=>x.Id).SingleAsync(cancellationToken);
  foreach(var resource in serviceResources)
  {
   resource.Status="COMPLETED";resource.CompletedAt=now;resource.IsActive=false;
   if(resource.VehicleId>0){var vehicle=await db.Vehicles.FindAsync([resource.VehicleId],cancellationToken);if(vehicle is not null)vehicle.VehicleStatusId=availableStatusId;}
   if(resource.DriverId.HasValue){var driver=await db.Drivers.FindAsync([resource.DriverId.Value],cancellationToken);if(driver is not null)driver.AvailabilityStatus="AVAILABLE";}
   db.FaultResourceStatusHistories.Add(new FaultResourceStatusHistory{ResourceAssignmentId=resource.Id,OldStatus="ARRIVED",NewStatus="COMPLETED",ChangedByUserId=userId,ChangedAt=now,Description="Başarısız yerinde müdahale tamamlandı; hizmet aracı garaja döndü."});
  }

  try
  {
   await resourceService.AssignRequiredAsync(fault,fault.Vehicle,null,null,userId,true,false,false,false,now,
    towTruckId:request.TowTruckId);
  }
  catch(InvalidOperationException exception)
  {
   await tx.RollbackAsync(cancellationToken);
   return Conflict(new{message=exception.Message});
  }

  db.FaultStatusHistories.Add(new FaultStatusHistory{FaultId=id,OldStatusId=fault.FaultStatusId,
   NewStatusId=departing.Id,ChangedByUserId=userId,ChangedByRoleId=roleId,ChangedAt=now,
   Description="Yerinde tamir başarısız olduğu için seçilen çekici yola çıkmaya hazırlanıyor.",IsSystemAction=false});
  fault.FaultStatusId=departing.Id;
  plan.OnSiteRepairPossible=false;plan.TowRequired=true;plan.ServiceVehicleRequired=false;
  plan.AutomationEnabled=true;plan.AutomationStatus="RESOURCE_DEPARTING";
  plan.NextAutomationAt=now.AddSeconds(await GetOperationStepSecondsAsync(cancellationToken));
  plan.LastAutomationError=null;
  await db.SaveChangesAsync(cancellationToken);
  await tx.CommitAsync(cancellationToken);
  return Ok(new{message="Çekici görevlendirildi; araç garaja alınacak."});
 }

 private async Task<int> GetOperationStepSecondsAsync(CancellationToken cancellationToken)
 {
  var value=await db.SystemSettings.AsNoTracking()
   .Where(x=>x.SettingKey=="presentation_dispatch_seconds"&&x.IsActive)
   .Select(x=>x.SettingValue).SingleOrDefaultAsync(cancellationToken);
  return int.TryParse(value,out var seconds)?Math.Clamp(seconds,1,3600):10;
 }
}
