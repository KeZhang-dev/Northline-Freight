using Microsoft.EntityFrameworkCore;
using Northline.Api.Common;
using Northline.Api.Data;
using Northline.Api.Dtos;
using Northline.Api.Entities;
using Northline.Api.Validation;

namespace Northline.Api.Services;

public interface IResourceQueryService
{
    Task<IReadOnlyList<DriverDto>> ListDriversAsync(DriverListQuery query, CancellationToken ct);
    Task<DriverDetailDto> GetDriverAsync(int driverId, CancellationToken ct);
    Task<IReadOnlyList<VehicleDto>> ListVehiclesAsync(VehicleListQuery query, CancellationToken ct);
    Task<VehicleDetailDto> GetVehicleAsync(int vehicleId, CancellationToken ct);
}

/// <summary>
/// Read-only driver and vehicle views. Availability is derived: operational status plus the
/// resource's active (Assigned) assignments. There is no stored availability flag.
/// </summary>
public class ResourceQueryService(
    NorthlineDbContext db,
    LicenceRules licenceRules,
    IAssignmentQueryService assignmentQueries,
    TimeProvider clock) : IResourceQueryService
{
    public async Task<IReadOnlyList<DriverDto>> ListDriversAsync(DriverListQuery q, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var query = db.Drivers.AsNoTracking()
            .Include(d => d.Assignments.Where(a => a.Status == AssignmentStatus.Assigned)).ThenInclude(a => a.Order)
            .AsQueryable();

        if (q.EmploymentStatus is { } status) query = query.Where(d => d.EmploymentStatus == status);
        if (QueryHelpers.LikePattern(q.Search) is { } like)
        {
            query = query.Where(d =>
                EF.Functions.ILike(d.DriverCode, like) ||
                EF.Functions.ILike(d.FirstName + " " + d.LastName, like) ||
                EF.Functions.ILike(d.Email, like) ||
                EF.Functions.ILike(d.Phone, like));
        }

        var drivers = await query.OrderBy(d => d.DriverCode).ToListAsync(ct);
        var result = drivers.Select(d => d.ToDto(licenceRules, ResourceSchedule.From(d.Assignments, now)));
        if (q.AvailableNow is { } availableNow)
        {
            result = result.Where(d => d.IsAvailableNow == availableNow);
        }
        return result.ToList();
    }

    public async Task<DriverDetailDto> GetDriverAsync(int driverId, CancellationToken ct)
    {
        var driver = await db.Drivers.AsNoTracking().SingleOrDefaultAsync(d => d.Id == driverId, ct)
            ?? throw DomainException.NotFound(ErrorCodes.DriverNotFound, $"Driver {driverId} was not found.");
        var assignments = await db.DispatchAssignments.WithDetails()
            .Where(a => a.DriverId == driverId)
            .OrderByDescending(a => a.StartAt).ThenByDescending(a => a.Id)
            .ToListAsync(ct);

        var schedule = ResourceSchedule.From(assignments, clock.GetUtcNow());
        return new DriverDetailDto(driver.ToDto(licenceRules, schedule), await assignmentQueries.MapAsync(assignments, ct));
    }

    public async Task<IReadOnlyList<VehicleDto>> ListVehiclesAsync(VehicleListQuery q, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var query = db.Vehicles.AsNoTracking()
            .Include(v => v.Assignments.Where(a => a.Status == AssignmentStatus.Assigned)).ThenInclude(a => a.Order)
            .AsQueryable();

        if (q.VehicleStatus is { } status) query = query.Where(v => v.VehicleStatus == status);
        if (q.VehicleType is { } type) query = query.Where(v => v.VehicleType == type);
        if (QueryHelpers.LikePattern(q.Search) is { } like)
        {
            query = query.Where(v =>
                EF.Functions.ILike(v.VehicleCode, like) ||
                EF.Functions.ILike(v.RegistrationNumber, like));
        }

        var vehicles = await query.OrderBy(v => v.VehicleCode).ToListAsync(ct);
        var result = vehicles.Select(v => v.ToDto(licenceRules, ResourceSchedule.From(v.Assignments, now)));
        if (q.AvailableNow is { } availableNow)
        {
            result = result.Where(v => v.IsAvailableNow == availableNow);
        }
        return result.ToList();
    }

    public async Task<VehicleDetailDto> GetVehicleAsync(int vehicleId, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw DomainException.NotFound(ErrorCodes.VehicleNotFound, $"Vehicle {vehicleId} was not found.");
        var assignments = await db.DispatchAssignments.WithDetails()
            .Where(a => a.VehicleId == vehicleId)
            .OrderByDescending(a => a.StartAt).ThenByDescending(a => a.Id)
            .ToListAsync(ct);

        var schedule = ResourceSchedule.From(assignments, clock.GetUtcNow());
        return new VehicleDetailDto(vehicle.ToDto(licenceRules, schedule), await assignmentQueries.MapAsync(assignments, ct));
    }
}
