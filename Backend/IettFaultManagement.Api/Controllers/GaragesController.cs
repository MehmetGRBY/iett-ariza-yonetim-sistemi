using IettFaultManagement.Api.Data;
using IettFaultManagement.Api.Dtos;
using IettFaultManagement.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace IettFaultManagement.Api.Controllers;
[ApiController,Authorize(Roles="Admin,Merkez Yetkilisi,Garaj Yetkilisi"),Route("api/garages")]
/// <summary>
/// Garajları; kapasite, aktif/pasif araç, sürücü, ekip ve araç tipi dağılımıyla raporlar.
/// Garaj yetkilisi sadece kendisine atanmış garajı görebilir.
/// </summary>
public sealed class GaragesController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var query = db.Garages.AsNoTracking().AsQueryable();
        // Garaj yetkilisinin başka garajlara ait operasyon verilerini görmesi engellenir.
        if (User.IsInRole("Garaj Yetkilisi"))
        {
            var garageId = User.GarageId();
            if (garageId is null) return Forbid();
            query = query.Where(garage => garage.Id == garageId.Value);
        }

        var garages = await query.OrderBy(garage => garage.Name).Select(garage => new
        {
            garage.Id, garage.Code, garage.Name, garage.Address, garage.VehicleCapacity, garage.IsActive,
            // Pasif araç da garajda fiziksel yer tuttuğundan toplam doluluğa dâhil edilir.
            TotalVehicles = garage.Vehicles.Count(),
            ActiveVehicles = garage.Vehicles.Count(vehicle => vehicle.IsActive),
            PassiveVehicles = garage.Vehicles.Count(vehicle => !vehicle.IsActive),
            Drivers = garage.Drivers.Count(driver => driver.IsActive),
            ReserveDrivers = garage.Drivers.Count(driver => driver.IsActive && driver.DriverType == "RESERVE"),
            Teams = garage.TechnicianTeams.Count(team => team.IsActive),
            Technicians = garage.TechnicianTeams.SelectMany(team => team.TeamMembers).Count(member => member.IsActive),
            HasManager = garage.AppUsers.Any(user => user.IsActive && user.Role.Name == "Garaj Yetkilisi")
        }).ToListAsync();

        return Ok(garages.Select(garage => new
        {
            garage.Id, garage.Code, garage.Name, garage.Address, garage.VehicleCapacity, garage.IsActive,
            garage.TotalVehicles, garage.ActiveVehicles, garage.PassiveVehicles,
            AvailableCapacity = Math.Max(garage.VehicleCapacity - garage.TotalVehicles, 0),
            OccupancyRate = garage.VehicleCapacity > 0 ? Math.Round(garage.TotalVehicles * 100m / garage.VehicleCapacity, 2) : 0m,
            garage.Drivers, garage.ReserveDrivers, garage.Teams, garage.Technicians, garage.HasManager
        }));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id)
    {
        if (User.IsInRole("Garaj Yetkilisi") && id != User.GarageId()) return Forbid();
        var garage = await db.Garages.AsNoTracking().Where(item => item.Id == id).Select(item => new
        {
            item.Id, item.Code, item.Name, item.Address, item.VehicleCapacity, item.IsActive,
            TotalVehicles = item.Vehicles.Count(),
            ActiveVehicles = item.Vehicles.Count(vehicle => vehicle.IsActive),
            PassiveVehicles = item.Vehicles.Count(vehicle => !vehicle.IsActive),
            Drivers = item.Drivers.Count(driver => driver.IsActive),
            NormalDrivers = item.Drivers.Count(driver => driver.IsActive && driver.DriverType != "RESERVE"),
            ReserveDrivers = item.Drivers.Count(driver => driver.IsActive && driver.DriverType == "RESERVE"),
            Manager = item.AppUsers.Where(user => user.IsActive && user.Role.Name == "Garaj Yetkilisi")
                .Select(user => new { user.PersonnelNumber, FullName = user.FirstName + " " + user.LastName }).FirstOrDefault(),
            VehicleTypes = item.Vehicles.GroupBy(vehicle => vehicle.VehicleType.Name)
                .Select(group => new { Type = group.Key, Count = group.Count() }).OrderByDescending(group => group.Count).ToList(),
            VehicleStatuses = item.Vehicles.GroupBy(vehicle => vehicle.VehicleStatus.Name)
                .Select(group => new { Status = group.Key, Count = group.Count() }).OrderByDescending(group => group.Count).ToList(),
            Teams = item.TechnicianTeams.Where(team => team.IsActive).OrderBy(team => team.Name).Select(team => new
            {
                team.Name, team.IsAvailable,
                ActiveMembers = team.TeamMembers.Count(member => member.IsActive),
                Members = team.TeamMembers.Where(member => member.IsActive).Select(member => new
                {
                    member.User.PersonnelNumber, FullName = member.User.FirstName + " " + member.User.LastName,
                    member.IsTeamLeader, member.WorkStatus
                }).ToList()
            })
        }).SingleOrDefaultAsync();

        if (garage is null) return NotFound();
        return Ok(new
        {
            garage.Id, garage.Code, garage.Name, garage.Address, garage.VehicleCapacity, garage.IsActive,
            garage.TotalVehicles, garage.ActiveVehicles, garage.PassiveVehicles,
            AvailableCapacity = Math.Max(garage.VehicleCapacity - garage.TotalVehicles, 0),
            OccupancyRate = garage.VehicleCapacity > 0 ? Math.Round(garage.TotalVehicles * 100m / garage.VehicleCapacity, 2) : 0m,
            garage.Drivers, garage.NormalDrivers, garage.ReserveDrivers, garage.Manager,
            garage.VehicleTypes, garage.VehicleStatuses, garage.Teams,
            Technicians = garage.Teams.Sum(team => team.ActiveMembers)
        });
    }

    /// <summary>Garaj kodunu değiştirmeden ad, adres ve fiziksel kapasiteyi günceller.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateGarageRequest request, CancellationToken cancellationToken)
    {
        var garage = await db.Garages.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (garage is null) return NotFound(new { message = "Garaj bulunamadı." });
        var name = request.Name.Trim();
        if (await db.Garages.AnyAsync(x => x.Id != id && x.Name.ToUpper() == name.ToUpper(), cancellationToken))
            return Conflict(new { message = "Bu garaj adı zaten kullanılıyor." });
        var vehicleCount = await db.Vehicles.CountAsync(x => x.GarageId == id, cancellationToken);
        if (request.VehicleCapacity < vehicleCount)
            return Conflict(new { message = $"Kapasite, garajdaki {vehicleCount} araçtan daha düşük olamaz." });
        garage.Name = name; garage.Address = request.Address?.Trim(); garage.VehicleCapacity = request.VehicleCapacity;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Bağlı aktif operasyon kaynağı yoksa garajı silmeden pasife alır.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:long}/active")]
    public async Task<IActionResult> ChangeActive(long id, ChangeGarageActiveRequest request, CancellationToken cancellationToken)
    {
        var garage = await db.Garages.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (garage is null) return NotFound(new { message = "Garaj bulunamadı." });
        if (garage.IsActive == request.IsActive) return NoContent();
        if (!request.IsActive)
        {
            var activeVehicles = await db.Vehicles.AnyAsync(x => x.GarageId == id && x.IsActive, cancellationToken);
            var activeDrivers = await db.Drivers.AnyAsync(x => x.GarageId == id && x.IsActive, cancellationToken);
            var activeUsers = await db.AppUsers.AnyAsync(x => x.GarageId == id && x.IsActive, cancellationToken);
            var activeTeams = await db.TechnicianTeams.AnyAsync(x => x.GarageId == id && x.IsActive, cancellationToken);
            if (activeVehicles || activeDrivers || activeUsers || activeTeams)
                return Conflict(new { message = "Garajda aktif araç, sürücü, kullanıcı veya teknik ekip bulunduğu için pasife alınamaz." });
        }
        garage.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
