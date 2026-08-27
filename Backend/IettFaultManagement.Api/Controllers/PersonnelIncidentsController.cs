using System.Text.Json;using IettFaultManagement.Api.Data;using IettFaultManagement.Api.Dtos;using IettFaultManagement.Api.Extensions;using IettFaultManagement.Api.Models.Database;using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using Microsoft.EntityFrameworkCore;
namespace IettFaultManagement.Api.Controllers;
[ApiController,Authorize(Roles="Admin,Merkez Yetkilisi,Garaj Yetkilisi"),Route("api/personnel-incidents")]
/// <summary>
/// Sefer sırasında hastalanan veya göreve devam edemeyen sürücü için olay kaydı açar;
/// yedek sürücü/hizmet aracı atar, gelecek görevleri devreder ve sağlık raporu süresini kaydeder.
/// </summary>
public sealed class PersonnelIncidentsController(ApplicationDbContext db):ControllerBase
{
 private IQueryable<PersonnelIncident> Scoped(){var q=db.PersonnelIncidents.AsQueryable();return User.IsInRole("Garaj Yetkilisi")&&User.GarageId() is long g?q.Where(x=>x.GarageId==g):q;}
 [HttpGet]public async Task<IActionResult> Get()=>Ok(await Scoped().AsNoTracking().OrderByDescending(x=>x.OccurredAt).Select(x=>new{x.Id,x.EventNumber,x.EventType,x.Status,x.Description,x.OccurredAt,x.AbsenceStartAt,x.ExpectedReturnAt,x.ReportStatus,x.ReportSubmittedAt,x.MedicalReportNumber,x.TransferredTaskCount,Driver=new{x.Driver.Id,x.Driver.PersonnelNumber,FullName=x.Driver.FirstName+" "+x.Driver.LastName},ReplacementDriver=x.ReplacementDriver==null?null:new{x.ReplacementDriver.Id,x.ReplacementDriver.PersonnelNumber,FullName=x.ReplacementDriver.FirstName+" "+x.ReplacementDriver.LastName},Garage=x.Garage.Name}).ToListAsync());
 [HttpPost]public async Task<IActionResult> Create(PersonnelIncidentRequest request){if(request.EventType is not("ILLNESS" or "EMERGENCY" or "UNFIT_FOR_DUTY"))return BadRequest(new{message="Geçerli olay türü seçin."});var now=DateTime.UtcNow;var active=await db.TaskAssignments.Include(x=>x.Driver).Include(x=>x.Vehicle).Include(x=>x.ServiceTask).FirstOrDefaultAsync(x=>x.IsActive&&x.DriverId==request.DriverId&&x.ServiceTask.IsActive&&x.ServiceTask.PlannedDepartureAt<=now&&x.ServiceTask.PlannedArrivalAt>=now);if(active is null)return BadRequest(new{message="Şoför şu anda aktif görevde değil."});if(User.IsInRole("Garaj Yetkilisi")&&active.Vehicle.GarageId!=User.GarageId())return Forbid();if(await db.PersonnelIncidents.AnyAsync(x=>x.DriverId==active.DriverId&&x.IsActive&&x.Status!="CANCELLED"&&(x.ReportStatus=="PENDING"||x.ExpectedReturnAt>now)))return Conflict(new{message="Şoför için devam eden olay var."});await using var tx=await db.Database.BeginTransactionAsync();var busy=db.TaskAssignments.Where(x=>x.IsActive&&x.ServiceTask.PlannedDepartureAt<=now&&x.ServiceTask.PlannedArrivalAt>=now).Select(x=>x.DriverId);var reserve=await db.Drivers.Where(x=>x.IsActive&&x.GarageId==active.Vehicle.GarageId&&x.DriverType=="RESERVE"&&x.AvailabilityStatus=="AVAILABLE"&&!busy.Contains(x.Id)).OrderBy(x=>x.TaskAssignments.Max(a=>(DateTime?)a.AssignedAt)??DateTime.MinValue).FirstOrDefaultAsync();var service=await db.Vehicles.Where(x=>x.IsActive&&x.GarageId==active.Vehicle.GarageId&&x.VehicleStatus.Code=="AVAILABLE"&&EF.Functions.ILike(x.VehicleType.Name,"%Hizmet%")).OrderBy(x=>x.DoorNumber).FirstOrDefaultAsync();var future=await db.TaskAssignments.Include(x=>x.ServiceTask).Where(x=>x.IsActive&&x.DriverId==active.DriverId&&x.ServiceTask.PlannedArrivalAt>now).ToListAsync();var ready=reserve is not null&&service is not null;var incident=new PersonnelIncident{EventNumber=$"POL-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",DriverId=active.DriverId,ReplacementDriverId=ready?reserve!.Id:null,VehicleId=active.VehicleId,ServiceVehicleId=ready?service!.Id:null,GarageId=active.Vehicle.GarageId,EventType=request.EventType,Status=ready?"DISPATCHED":"WAITING_REPLACEMENT",Description=request.Description.Trim(),OccurredAt=now,AbsenceStartAt=now,ExpectedReturnAt=null,ReportStatus="PENDING",DispatchedAt=ready?now:null,ArrivalDueAt=ready?now.AddMinutes(5):null,TransferredTaskCount=ready?future.Count:0,CreatedByUserId=User.UserId(),CreatedAt=now,IsActive=true};db.PersonnelIncidents.Add(incident);active.Driver.AvailabilityStatus="ON_LEAVE";if(ready){reserve!.AvailabilityStatus="ON_DUTY";foreach(var old in future){old.IsActive=false;old.EndedAt=now;db.TaskAssignments.Add(new TaskAssignment{ServiceTaskId=old.ServiceTaskId,VehicleId=old.VehicleId,DriverId=reserve.Id,AssignmentType="REPLACEMENT",AssignedByUserId=User.UserId(),AssignedAt=now,IsActive=true,Description="Personel olayı nedeniyle yedek şoföre devredildi."});}service!.VehicleStatusId=(await db.VehicleStatuses.SingleAsync(x=>x.Code=="ON_DUTY")).Id;}db.AuditLogs.Add(new AuditLog{UserId=User.UserId(),Action="PERSONNEL_INCIDENT_CREATED",EntityType="personnel_incidents",NewValues=JsonSerializer.Serialize(new{incident.EventNumber,incident.DriverId,incident.ReplacementDriverId,incident.TransferredTaskCount}),Description="Personel olayı ve görev devri oluşturuldu.",CreatedAt=now});await db.SaveChangesAsync();await tx.CommitAsync();return Created($"/api/personnel-incidents/{incident.Id}",new{incident.Id,incident.EventNumber,incident.TransferredTaskCount});}
 [HttpPut("{id:long}/report")]
 public async Task<IActionResult> Report(long id,PersonnelReportRequest request)
 {
  var incident=await Scoped().SingleOrDefaultAsync(x=>x.Id==id&&x.IsActive);
  if(incident is null)return NotFound();
  if(request.ReportEndDate<request.ReportStartDate)
   return BadRequest(new{message="Rapor bitiş tarihi başlangıçtan önce olamaz."});

  var now=DateTime.UtcNow;
  incident.AbsenceStartAt=request.ReportStartDate!.Value
   .ToDateTime(TimeOnly.MinValue,DateTimeKind.Local).ToUniversalTime();
  // Raporun son günü de izin kapsamındadır; sürücü ertesi gün 00.00 itibarıyla döner.
  incident.ExpectedReturnAt=request.ReportEndDate!.Value.AddDays(1)
   .ToDateTime(TimeOnly.MinValue,DateTimeKind.Local).ToUniversalTime();
  incident.MedicalReportNumber=request.ReportNumber?.Trim();
  incident.ReportStatus="SUBMITTED";
  incident.ReportSubmittedAt=now;
  if(!string.IsNullOrWhiteSpace(request.Notes))incident.Description+=" | Rapor: "+request.Notes.Trim();

  // Rapor tarihi sonradan değiştirildiğinde de sürücünün görev dışı durumu korunur.
  var driver=await db.Drivers.SingleAsync(x=>x.Id==incident.DriverId);
  if(incident.ExpectedReturnAt>now)driver.AvailabilityStatus="ON_LEAVE";

  // Sağlık raporu girişi denetlenebilir bir işlem olduğu için kullanıcı ve tarih bilgisiyle loglanır.
  db.AuditLogs.Add(new AuditLog
  {
   UserId=User.UserId(),Action="PERSONNEL_REPORT_SUBMITTED",EntityType="personnel_incidents",EntityId=incident.Id,
   NewValues=JsonSerializer.Serialize(new{incident.ReportStatus,incident.AbsenceStartAt,incident.ExpectedReturnAt,incident.MedicalReportNumber}),
   Description="Personel sağlık raporu kaydedildi.",CreatedAt=now
  });
  await db.SaveChangesAsync();
  return NoContent();
 }
}
