using Microsoft.EntityFrameworkCore;
using Northline.Api.Common;
using Northline.Api.Data;
using Northline.Api.Dtos;
using Northline.Api.Entities;

namespace Northline.Api.Services;

public interface ISummaryService
{
    Task<DispatchSummaryDto> GetDispatchSummaryAsync(CancellationToken ct);
    Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken ct);
    Task<DriverProfileDto> GetDriverProfileAsync(int userId, CancellationToken ct);
}

public class SummaryService(NorthlineDbContext db, TimeProvider clock) : ISummaryService
{
    public async Task<DispatchSummaryDto> GetDispatchSummaryAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        var pending = await db.TransportOrders.CountAsync(o => o.Status == OrderStatus.Pending, ct);
        var eligiblePending = await db.TransportOrders.CountAsync(o =>
            o.Status == OrderStatus.Pending && o.DeliveryDeadline > now && o.PickupAt < o.DeliveryDeadline, ct);
        var urgentPending = await db.TransportOrders.CountAsync(o =>
            o.Status == OrderStatus.Pending && o.Priority == OrderPriority.Urgent && o.DeliveryDeadline > now, ct);
        var assignedJobs = await db.DispatchAssignments.CountAsync(a => a.Status == AssignmentStatus.Assigned, ct);

        // "Available now" = operational status Active and no active assignment covering the current time.
        var busyDriverIds = db.DispatchAssignments
            .Where(a => a.Status == AssignmentStatus.Assigned && a.StartAt <= now && now < a.EndAt)
            .Select(a => a.DriverId);
        var busyVehicleIds = db.DispatchAssignments
            .Where(a => a.Status == AssignmentStatus.Assigned && a.StartAt <= now && now < a.EndAt)
            .Select(a => a.VehicleId);

        var totalDrivers = await db.Drivers.CountAsync(ct);
        var activeDrivers = await db.Drivers.CountAsync(d => d.EmploymentStatus == EmploymentStatus.Active, ct);
        var availableDrivers = await db.Drivers.CountAsync(d =>
            d.EmploymentStatus == EmploymentStatus.Active && !busyDriverIds.Contains(d.Id), ct);

        var totalVehicles = await db.Vehicles.CountAsync(ct);
        var activeVehicles = await db.Vehicles.CountAsync(v => v.VehicleStatus == VehicleStatus.Active, ct);
        var availableVehicles = await db.Vehicles.CountAsync(v =>
            v.VehicleStatus == VehicleStatus.Active && !busyVehicleIds.Contains(v.Id), ct);

        return new DispatchSummaryDto(now, pending, eligiblePending, urgentPending, assignedJobs,
            availableDrivers, activeDrivers, totalDrivers, availableVehicles, activeVehicles, totalVehicles);
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var weekAhead = now.AddDays(7);

        var orders = await db.TransportOrders.GroupBy(o => o.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var assignments = await db.DispatchAssignments.GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var drivers = await db.Drivers.GroupBy(d => d.EmploymentStatus).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var vehicles = await db.Vehicles.GroupBy(v => v.VehicleStatus).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var nextWeek = await db.DispatchAssignments.CountAsync(a =>
            a.Status == AssignmentStatus.Assigned && a.StartAt >= now && a.StartAt < weekAhead, ct);

        static IReadOnlyList<CountByLabel> All<TEnum>(IEnumerable<(TEnum Key, int Count)> rows) where TEnum : struct, Enum =>
            Enum.GetValues<TEnum>()
                .Select(v => new CountByLabel(v.ToString(), rows.Where(r => r.Key.Equals(v)).Sum(r => r.Count)))
                .ToList();

        return new DashboardStatsDto(now,
            "Figures are calculated from the seeded development dataset and are for demonstration only.",
            All(orders.Select(r => (r.Key, r.Count))),
            All(assignments.Select(r => (r.Key, r.Count))),
            All(drivers.Select(r => (r.Key, r.Count))),
            All(vehicles.Select(r => (r.Key, r.Count))),
            nextWeek);
    }

    public async Task<DriverProfileDto> GetDriverProfileAsync(int userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.Driver).SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw DomainException.NotFound(ErrorCodes.NotFound, "User not found.");

        return new DriverProfileDto(user.ToDto(), user.Driver?.ToRef(),
            "The Driver Portal is not part of this release. Your dispatcher will contact you with job details.");
    }
}
