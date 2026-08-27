using IettFaultManagement.Api.Data;using IettFaultManagement.Api.Dtos;using IettFaultManagement.Api.Extensions;using IettFaultManagement.Api.Models.Database;using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using Microsoft.EntityFrameworkCore;
namespace IettFaultManagement.Api.Controllers;
[ApiController,Authorize(Roles="Admin,Garaj Yetkilisi"),Route("api/employees")]
/// <summary>
/// Sürücü ve teknisyenlerin garaj bazlı listelenmesini ve eklenmesini yönetir.
/// Admin tüm garajlarda, garaj yetkilisi ise yalnızca kendi garajında işlem yapabilir.
/// </summary>
public sealed class EmployeesController(ApplicationDbContext db):ControllerBase
{

 private long? Scope(long? requested)=>User.IsInRole("Admin")?requested:User.GarageId();
 [HttpGet("drivers")]
 public async Task<IActionResult> Drivers([FromQuery]long? garageId=null)
 {
  var scope=Scope(garageId);
  var now=DateTime.UtcNow;
  var q=db.Drivers.AsNoTracking();
  if(scope.HasValue)q=q.Where(x=>x.GarageId==scope);

  // ON_LEAVE rozetinin yanında bu duruma neden olan olayın özeti de gönderilir.
  // Böylece ön yüz yalnızca durum kodunu değil, izin/raporun dayanağını gösterebilir.
  return Ok(await q.OrderBy(x=>x.Garage!.Name).ThenBy(x=>x.PersonnelNumber).Select(x=>new
  {
   x.Id,x.PersonnelNumber,x.FirstName,x.LastName,x.GenderCode,x.DriverType,
   x.AvailabilityStatus,x.IsActive,x.GarageId,Garage=x.Garage!=null?x.Garage.Name:null,
   LeaveEventNumber=db.PersonnelIncidents
    .Where(i=>i.DriverId==x.Id&&i.IsActive&&i.Status!="CANCELLED"&&i.AbsenceStartAt<=now&&
     (i.ReportStatus=="PENDING"||!i.ExpectedReturnAt.HasValue||i.ExpectedReturnAt>now))
    .OrderByDescending(i=>i.OccurredAt).Select(i=>i.EventNumber).FirstOrDefault(),
   LeaveReason=db.PersonnelIncidents
    .Where(i=>i.DriverId==x.Id&&i.IsActive&&i.Status!="CANCELLED"&&i.AbsenceStartAt<=now&&
     (i.ReportStatus=="PENDING"||!i.ExpectedReturnAt.HasValue||i.ExpectedReturnAt>now))
    .OrderByDescending(i=>i.OccurredAt).Select(i=>i.Description).FirstOrDefault(),
   LeaveUntil=db.PersonnelIncidents
    .Where(i=>i.DriverId==x.Id&&i.IsActive&&i.Status!="CANCELLED"&&i.AbsenceStartAt<=now&&
     (i.ReportStatus=="PENDING"||!i.ExpectedReturnAt.HasValue||i.ExpectedReturnAt>now))
    .OrderByDescending(i=>i.OccurredAt).Select(i=>i.ExpectedReturnAt).FirstOrDefault()
  }).ToListAsync());
 }
 /// <summary>Bir sürücünün temel bilgilerini, görev geçmişini ve bildirdiği son arızaları döndürür.</summary>
 [HttpGet("drivers/{id:long}")]
 public async Task<IActionResult> DriverDetails(long id)
 {
  var now=DateTime.UtcNow;
  var driver=await db.Drivers.AsNoTracking().Where(x=>x.Id==id).Select(x=>new
  {
   x.Id,x.PersonnelNumber,x.FirstName,x.LastName,x.GenderCode,x.DriverType,
   x.AvailabilityStatus,x.IsActive,x.GarageId,Garage=x.Garage==null?null:x.Garage.Name,
   TaskCount=x.TaskAssignments.Count(),
   ActiveTaskCount=x.TaskAssignments.Count(a=>a.IsActive&&a.ServiceTask.PlannedArrivalAt>now),
   FaultCount=x.Faults.Count(),
   CurrentIncident=db.PersonnelIncidents
    .Where(i=>i.DriverId==x.Id&&i.IsActive&&i.Status!="CANCELLED"&&i.AbsenceStartAt<=now&&
     (i.ReportStatus=="PENDING"||!i.ExpectedReturnAt.HasValue||i.ExpectedReturnAt>now))
    .OrderByDescending(i=>i.OccurredAt)
    .Select(i=>new{i.EventNumber,i.EventType,i.Description,i.ReportStatus,i.AbsenceStartAt,i.ExpectedReturnAt})
    .FirstOrDefault(),
   RecentTasks=x.TaskAssignments.OrderByDescending(a=>a.AssignedAt).Take(10)
    .Select(a=>new{a.ServiceTask.TaskNumber,Route=a.ServiceTask.Route.Code,Vehicle=a.Vehicle.DoorNumber,a.AssignmentType,a.AssignedAt,a.EndedAt,a.IsActive}).ToList(),
   RecentFaults=x.Faults.OrderByDescending(f=>f.OccurredAt).Take(10)
    .Select(f=>new{f.FaultNumber,Vehicle=f.Vehicle.DoorNumber,Category=f.FaultCategory.Name,Status=f.FaultStatus.Name,f.OccurredAt}).ToList()
  }).SingleOrDefaultAsync();
  if(driver is null)return NotFound();
  if(!User.IsInRole("Admin")&&driver.GarageId!=User.GarageId())return Forbid();
  return Ok(driver);
 }
 [HttpGet("technicians")]public async Task<IActionResult> Technicians([FromQuery]long? garageId=null){var scope=Scope(garageId);var q=db.TeamMembers.AsNoTracking();if(scope.HasValue)q=q.Where(x=>x.Team.GarageId==scope);return Ok(await q.OrderBy(x=>x.Team.Garage.Name).ThenBy(x=>x.Team.Name).ThenBy(x=>x.User.PersonnelNumber).Select(x=>new{x.Id,x.UserId,x.User.PersonnelNumber,x.User.FirstName,x.User.LastName,x.WorkStatus,x.IsTeamLeader,x.IsActive,Team=new{x.Team.Id,x.Team.Name,x.Team.IsAvailable},Garage=new{x.Team.Garage.Id,x.Team.Garage.Name}}).ToListAsync());}
 /// <summary>Garaj kapsamındaki ekipleri üye sayıları ve aktif arıza atamalarıyla listeler.</summary>
 [HttpGet("technician-teams")]public async Task<IActionResult> TechnicianTeams([FromQuery]long? garageId=null){var scope=Scope(garageId);var q=db.TechnicianTeams.AsNoTracking();if(scope.HasValue)q=q.Where(x=>x.GarageId==scope);return Ok(await q.OrderBy(x=>x.Garage.Name).ThenBy(x=>x.Name).Select(x=>new{x.Id,x.Name,x.GarageId,Garage=x.Garage.Name,x.IsActive,x.IsAvailable,x.LastAssignedAt,MemberCount=x.TeamMembers.Count(m=>m.IsActive),ActiveFaultCount=x.FaultAssignments.Count(a=>a.IsActive&&a.Fault.ClosedAt==null)}).ToListAsync());}
 /// <summary>Yeni teknisyeni iki kişiden az üyeli ekibe otomatik yerleştirir; gerekirse yeni ekip oluşturur.</summary>
 [HttpPost("technicians")]public async Task<IActionResult> CreateTechnician(CreateTechnicianRequest request){var garageId=User.IsInRole("Admin")?request.GarageId:User.GarageId();if(!garageId.HasValue||!await db.Garages.AnyAsync(x=>x.Id==garageId&&x.IsActive))return BadRequest(new{message="Geçerli garaj seçin."});var garage=await db.Garages.Where(x=>x.Id==garageId).Select(x=>new{x.Code,x.Name}).SingleAsync();var team=await db.TechnicianTeams.Where(x=>x.GarageId==garageId&&x.IsActive&&x.TeamMembers.Count(m=>m.IsActive)<2).OrderBy(x=>x.TeamMembers.Count(m=>m.IsActive)).ThenBy(x=>x.Name).FirstOrDefaultAsync();if(team is null){var teamNumber=await db.TechnicianTeams.CountAsync(x=>x.GarageId==garageId)+1;team=new TechnicianTeam{Name=$"Ekip {teamNumber}",GarageId=garageId.Value,IsAvailable=true,IsActive=true,CreatedAt=DateTime.UtcNow};db.TechnicianTeams.Add(team);}var role=await db.Roles.SingleOrDefaultAsync(x=>x.Name=="Teknisyen"&&x.IsActive);if(role is null)return Conflict(new{message="Aktif Teknisyen rolü bulunamadı."});var sequence=await db.AppUsers.CountAsync(x=>x.GarageId==garageId&&x.RoleId==role.Id)+1;string number;do{number=$"TKN-{garage.Code}-{sequence++:000}";}while(await db.AppUsers.AnyAsync(x=>x.PersonnelNumber==number));var user=new AppUser{PersonnelNumber=number,NormalizedPersonnelNumber=number,FirstName=request.FirstName.Trim(),LastName=request.LastName.Trim(),GenderCode=request.GenderCode,PasswordHash="PERSONNEL_LOGIN_DISABLED",RoleId=role.Id,GarageId=garageId,IsActive=true,CreatedAt=DateTime.UtcNow,SecurityStamp=Guid.NewGuid()};db.AppUsers.Add(user);db.TeamMembers.Add(new TeamMember{Team=team,User=user,IsTeamLeader=!await db.TeamMembers.AnyAsync(x=>x.TeamId==team.Id&&x.IsActive),JoinedAt=DateTime.UtcNow,IsActive=true,WorkStatus="AVAILABLE"});await db.SaveChangesAsync();return Created($"/api/employees/technicians/{user.Id}",new{user.Id,user.PersonnelNumber,Team=team.Name,Garage=garage.Name});}
 /// <summary>Aktif görevi olmayan teknisyeni pasife alır veya yeniden etkinleştirir.</summary>
 [HttpPut("technicians/{memberId:long}/active")]public async Task<IActionResult> ToggleTechnician(long memberId){var member=await db.TeamMembers.Include(x=>x.Team).Include(x=>x.User).SingleOrDefaultAsync(x=>x.Id==memberId);if(member is null)return NotFound();if(!User.IsInRole("Admin")&&member.Team.GarageId!=User.GarageId())return Forbid();if(member.IsActive&&await db.FaultAssignments.AnyAsync(x=>x.TeamId==member.TeamId&&x.IsActive&&x.Fault.ClosedAt==null))return Conflict(new{message="Teknisyenin ekibinde devam eden arıza görevi var."});member.IsActive=!member.IsActive;member.WorkStatus=member.IsActive?"AVAILABLE":"PASSIVE";member.User.IsActive=member.IsActive;member.LeftAt=member.IsActive?null:DateTime.UtcNow;if(member.IsActive)member.JoinedAt=DateTime.UtcNow;await db.SaveChangesAsync();return Ok(new{member.Id,member.IsActive,member.WorkStatus});}
 [HttpPost("drivers")]public async Task<IActionResult> CreateDriver(CreateDriverRequest request){var garageId=User.IsInRole("Admin")?request.GarageId:User.GarageId();if(!garageId.HasValue||!await db.Garages.AnyAsync(x=>x.Id==garageId&&x.IsActive))return BadRequest(new{message="Geçerli garaj seçin."});var code=await db.Garages.Where(x=>x.Id==garageId).Select(x=>x.Code).SingleAsync();var sequence=await db.Drivers.CountAsync(x=>x.GarageId==garageId)+1;string number;do{number=$"DRV-{code}-{(request.DriverType=="RESERVE"?"Y":"N")}-{sequence++:000}";}while(await db.Drivers.AnyAsync(x=>x.PersonnelNumber==number));var d=new Driver{PersonnelNumber=number,FirstName=request.FirstName.Trim(),LastName=request.LastName.Trim(),GenderCode=request.GenderCode,GarageId=garageId,DriverType=request.DriverType,AvailabilityStatus="AVAILABLE",IsActive=true,CreatedAt=DateTime.UtcNow};db.Drivers.Add(d);await db.SaveChangesAsync();return Created($"/api/employees/drivers/{d.Id}",new{d.Id,d.PersonnelNumber});}
 [HttpPut("drivers/{id:long}/active")]public async Task<IActionResult> ToggleDriver(long id){var d=await db.Drivers.FindAsync(id);if(d is null)return NotFound();if(!User.IsInRole("Admin")&&d.GarageId!=User.GarageId())return Forbid();if(d.IsActive&&await db.TaskAssignments.AnyAsync(x=>x.DriverId==id&&x.IsActive&&x.ServiceTask.PlannedArrivalAt>DateTime.UtcNow))return Conflict(new{message="Şoförün aktif veya planlanmış görevleri var."});d.IsActive=!d.IsActive;d.AvailabilityStatus=d.IsActive?"AVAILABLE":"PASSIVE";await db.SaveChangesAsync();return Ok(new{d.Id,d.IsActive,d.AvailabilityStatus});}
}
