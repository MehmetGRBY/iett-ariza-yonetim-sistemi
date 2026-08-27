using IettFaultManagement.Api.Data;
using IettFaultManagement.Api.Dtos;
using IettFaultManagement.Api.Extensions;
using IettFaultManagement.Api.Models.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Merkez Yetkilisi,Garaj Yetkilisi")]
[Route("api/vehicles")]
/// <summary>
/// Araç listesini sayfalı ve filtreli sunar; araç detayı ile arıza/geçmiş bilgilerini getirir.
/// Garaj yetkilisi sorguları JWT'deki garageId ile otomatik olarak kendi garajına sınırlanır.
/// </summary>
public sealed class VehiclesController(ApplicationDbContext db) : ControllerBase
{
    /// <summary>Araç düzenleme formundaki ilişkili seçim listelerini tek istekte getirir.</summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("management-options")]
    public async Task<IActionResult> ManagementOptions(CancellationToken cancellationToken) => Ok(new
    {
        Garages = await db.Garages.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name, x.VehicleCapacity }).ToListAsync(cancellationToken),
        VehicleTypes = await db.VehicleTypes.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name }).ToListAsync(cancellationToken),
        FuelTypes = await db.FuelTypes.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name }).ToListAsync(cancellationToken),
        Statuses = await db.VehicleStatuses.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder)
            .Select(x => new { x.Id, x.Code, x.Name }).ToListAsync(cancellationToken)
    });

    [HttpGet]
    public async Task<ActionResult<PagedResponse<VehicleListResponse>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] long? garageId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? search = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Vehicles.AsNoTracking().AsQueryable();

        // Garaj yetkilisinin URL'yi elle değiştirerek başka garajı istemesi backend'de engellenir.
        if (User.IsInRole("Garaj Yetkilisi"))
        {
            var ownGarageId = User.GarageId();
            if (!ownGarageId.HasValue) return Forbid();
            query = query.Where(x => x.GarageId == ownGarageId);
        }
        else if (garageId.HasValue)
        {
            query = query.Where(x => x.GarageId == garageId);
        }

        if (isActive.HasValue)
            query = query.Where(x => x.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.DoorNumber, pattern) ||
                EF.Functions.ILike(x.Plate, pattern) ||
                EF.Functions.ILike(x.Brand, pattern) ||
                EF.Functions.ILike(x.Model, pattern));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(x => x.Garage.Name)
            .ThenBy(x => x.DoorNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new VehicleListResponse(
                x.Id, x.DoorNumber, x.Plate, x.Brand, x.Model, x.ModelYear,
                x.VehicleType.Name, x.FuelType.Name, x.CurrentMileage,
                x.Garage.Name, x.VehicleStatus.Name, x.Capacity, x.IsActive))
            .ToListAsync();

        return Ok(new PagedResponse<VehicleListResponse>(
            items, page, pageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize)));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var vehicle = await db.Vehicles.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id, x.DoorNumber, x.Plate, x.Brand, x.Model, x.ModelYear,
                x.VehicleTypeId, VehicleType = x.VehicleType.Name,
                x.FuelTypeId, FuelType = x.FuelType.Name,
                x.CurrentMileage,
                Garage = x.Garage.Name,
                x.GarageId,
                x.VehicleStatusId, Status = x.VehicleStatus.Name,
                x.DutyType,
                x.Capacity,
                x.IsActive,
                x.CreatedAt,
                x.DeactivatedAt,
                x.DeactivationReason
            })
            .SingleOrDefaultAsync();

        if (vehicle is null) return NotFound();
        if (User.IsInRole("Garaj Yetkilisi") && vehicle.GarageId != User.GarageId()) return Forbid();

        var faultHistory = await db.Faults.AsNoTracking()
            .Where(x => x.VehicleId == id)
            .OrderByDescending(x => x.OccurredAt)
            .Select(x => new
            {
                x.Id, x.FaultNumber,
                Category = x.FaultCategory.Name,
                Status = x.FaultStatus.Name,
                x.OccurredAt,
                x.ClosedAt
            })
            .ToListAsync();

        var garageHistory = await db.VehicleGarageHistories.AsNoTracking()
            .Where(x => x.VehicleId == id)
            .OrderByDescending(x => x.ChangedAt)
            .Select(x => new
            {
                OldGarage = x.OldGarage != null ? x.OldGarage.Name : null,
                NewGarage = x.NewGarage.Name,
                x.Description,
                x.ChangedAt,
                ChangedBy = x.ChangedByUser.FirstName + " " + x.ChangedByUser.LastName
            })
            .ToListAsync();

        var statusHistory = await db.VehicleStatusHistories.AsNoTracking()
            .Where(x => x.VehicleId == id)
            .OrderByDescending(x => x.ChangedAt)
            .Select(x => new
            {
                OldStatus = x.OldStatus != null ? x.OldStatus.Name : null,
                NewStatus = x.NewStatus.Name,
                x.Description,
                x.ChangedAt,
                x.FaultId,
                ChangedBy = x.ChangedByUser.FirstName + " " + x.ChangedByUser.LastName
            })
            .ToListAsync();

        return Ok(new { vehicle, faultHistory, garageHistory, statusHistory });
    }

    /// <summary>Admin, kapı numarası dışındaki araç bilgilerini ve operasyonel durumunu günceller.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateVehicleRequest request, CancellationToken cancellationToken)
    {
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (vehicle is null) return NotFound(new { message = "Araç bulunamadı." });

        var plate = request.Plate.Trim().ToUpperInvariant();
        if (await db.Vehicles.AnyAsync(x => x.Id != id && x.Plate.ToUpper() == plate, cancellationToken))
            return Conflict(new { message = "Bu plaka başka bir araçta kullanılıyor." });
        if (!await db.VehicleTypes.AnyAsync(x => x.Id == request.VehicleTypeId && x.IsActive, cancellationToken))
            return BadRequest(new { message = "Geçerli araç tipi seçin." });
        if (!await db.FuelTypes.AnyAsync(x => x.Id == request.FuelTypeId && x.IsActive, cancellationToken))
            return BadRequest(new { message = "Geçerli yakıt tipi seçin." });
        if (!await db.VehicleStatuses.AnyAsync(x => x.Id == request.VehicleStatusId && x.IsActive, cancellationToken))
            return BadRequest(new { message = "Geçerli araç durumu seçin." });
        var targetGarage = await db.Garages.SingleOrDefaultAsync(x => x.Id == request.GarageId && x.IsActive, cancellationToken);
        if (targetGarage is null) return BadRequest(new { message = "Geçerli ve aktif bir garaj seçin." });
        if (request.CurrentMileage < vehicle.CurrentMileage)
            return BadRequest(new { message = $"Kilometre mevcut {vehicle.CurrentMileage:N0} km değerinden küçük olamaz." });

        var garageChanged = vehicle.GarageId != request.GarageId;
        if (garageChanged && await db.Vehicles.CountAsync(x => x.GarageId == targetGarage.Id, cancellationToken) >= targetGarage.VehicleCapacity)
            return Conflict(new { message = "Hedef garajın boş araç kapasitesi bulunmuyor." });

        var now = DateTime.UtcNow;
        if (garageChanged)
            db.VehicleGarageHistories.Add(new VehicleGarageHistory { VehicleId = id, OldGarageId = vehicle.GarageId,
                NewGarageId = targetGarage.Id, ChangedByUserId = User.UserId(), ChangedAt = now,
                Description = request.ChangeDescription.Trim() });
        if (vehicle.VehicleStatusId != request.VehicleStatusId)
            db.VehicleStatusHistories.Add(new VehicleStatusHistory { VehicleId = id, OldStatusId = vehicle.VehicleStatusId,
                NewStatusId = request.VehicleStatusId!.Value, ChangedByUserId = User.UserId(), ChangedAt = now,
                Description = request.ChangeDescription.Trim() });

        vehicle.Plate = plate;
        vehicle.Brand = request.Brand.Trim(); vehicle.Model = request.Model.Trim(); vehicle.ModelYear = request.ModelYear;
        vehicle.VehicleTypeId = request.VehicleTypeId!.Value; vehicle.FuelTypeId = request.FuelTypeId!.Value;
        vehicle.CurrentMileage = request.CurrentMileage; vehicle.GarageId = request.GarageId!.Value;
        vehicle.VehicleStatusId = request.VehicleStatusId!.Value; vehicle.DutyType = request.DutyType?.Trim();
        vehicle.Capacity = request.Capacity;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Araç kaydını silmeden aktif veya pasif yapar ve durum geçmişini korur.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:long}/active")]
    public async Task<IActionResult> ChangeActive(long id, ChangeVehicleActiveRequest request, CancellationToken cancellationToken)
    {
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (vehicle is null) return NotFound(new { message = "Araç bulunamadı." });
        if (vehicle.IsActive == request.IsActive) return NoContent();

        var now = DateTime.UtcNow;
        if (!request.IsActive)
        {
            if (await db.Faults.AnyAsync(x => x.VehicleId == id && x.IsActive && x.ClosedAt == null, cancellationToken))
                return Conflict(new { message = "Araçta açık arıza bulunduğu için pasife alınamaz." });
            if (await db.TaskAssignments.AnyAsync(x => x.VehicleId == id && x.IsActive &&
                    x.ServiceTask.PlannedArrivalAt > now, cancellationToken))
                return Conflict(new { message = "Aracın aktif veya planlanmış görevi bulunduğu için pasife alınamaz." });
        }

        var statusCode = request.IsActive ? "AVAILABLE" : "OUT_OF_SERVICE";
        var newStatus = await db.VehicleStatuses.SingleAsync(x => x.Code == statusCode && x.IsActive, cancellationToken);
        if (vehicle.VehicleStatusId != newStatus.Id)
            db.VehicleStatusHistories.Add(new VehicleStatusHistory { VehicleId = id, OldStatusId = vehicle.VehicleStatusId,
                NewStatusId = newStatus.Id, ChangedByUserId = User.UserId(), ChangedAt = now,
                Description = request.Reason.Trim() });

        vehicle.IsActive = request.IsActive; vehicle.VehicleStatusId = newStatus.Id;
        vehicle.DeactivatedAt = request.IsActive ? null : now;
        vehicle.DeactivationReason = request.IsActive ? null : request.Reason.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("by-door-number/{doorNumber}")]
    public async Task<IActionResult> ByDoorNumber(string doorNumber)
    {
        var item = await db.Vehicles.AsNoTracking()
            .Where(x => x.DoorNumber.ToUpper() == doorNumber.Trim().ToUpper())
            .Select(x => new
            {
                x.Id, x.DoorNumber, x.Plate, x.Brand, x.Model, x.ModelYear,
                VehicleType = x.VehicleType.Name,
                Garage = x.Garage.Name,
                x.GarageId,
                Status = x.VehicleStatus.Name,
                x.CurrentMileage,
                x.Capacity,
                x.IsActive
            })
            .SingleOrDefaultAsync();

        if (item is null) return NotFound();
        if (User.IsInRole("Garaj Yetkilisi") && item.GarageId != User.GarageId()) return Forbid();
        return Ok(item);
    }
}
