using Microsoft.EntityFrameworkCore;
using Northline.Api.Common;
using Northline.Api.Data;
using Northline.Api.Dtos;
using Northline.Api.Entities;
using Northline.Api.Validation;

namespace Northline.Api.Services;

public interface IOrderQueryService
{
    Task<PagedResult<OrderDto>> ListAsync(OrderListQuery query, CancellationToken ct);
    Task<OrderDetailDto> GetAsync(int orderId, CancellationToken ct);
    Task<CandidatesDto> GetCandidatesAsync(int orderId, CandidatesQuery query, CancellationToken ct);
}

public class OrderQueryService(
    NorthlineDbContext db,
    DispatchRuleEvaluator evaluator,
    LicenceRules licenceRules,
    IAssignmentQueryService assignmentQueries,
    TimeProvider clock) : IOrderQueryService
{
    public async Task<PagedResult<OrderDto>> ListAsync(OrderListQuery q, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var query = OrdersWithActiveAssignment();

        if (q.Status is { } status) query = query.Where(o => o.Status == status);
        if (q.Priority is { } priority) query = query.Where(o => o.Priority == priority);
        if (q.EligibleOnly == true)
        {
            // Same conditions as DispatchRuleEvaluator.EvaluateOrderForAssignment (BR-01).
            query = query.Where(o => o.Status == OrderStatus.Pending && o.DeliveryDeadline > now && o.PickupAt < o.DeliveryDeadline);
        }
        if (QueryHelpers.LikePattern(q.Search) is { } like)
        {
            query = query.Where(o =>
                EF.Functions.ILike(o.OrderNumber, like) ||
                EF.Functions.ILike(o.CustomerName, like) ||
                EF.Functions.ILike(o.PickupLocation, like) ||
                EF.Functions.ILike(o.DeliveryLocation, like) ||
                EF.Functions.ILike(o.CargoDescription, like));
        }

        var ordered = q.Sort switch
        {
            OrderSortField.DeliveryDeadline => query.OrderByDir(o => o.DeliveryDeadline, q.Dir),
            OrderSortField.Priority => query.OrderByDir(QueryHelpers.PriorityRank, q.Dir),
            OrderSortField.CargoWeightKg => query.OrderByDir(o => o.CargoWeightKg, q.Dir),
            OrderSortField.OrderNumber => query.OrderByDir(o => o.OrderNumber, q.Dir),
            OrderSortField.CustomerName => query.OrderByDir(o => o.CustomerName, q.Dir),
            _ => query.OrderByDir(o => o.PickupAt, q.Dir),
        };

        return await ordered.ThenBy(o => o.Id).ToPagedAsync(q,
            items => Task.FromResult<IReadOnlyList<OrderDto>>(items.Select(o => MapOrder(o, now)).ToList()), ct);
    }

    public async Task<OrderDetailDto> GetAsync(int orderId, CancellationToken ct)
    {
        var order = await FindOrderAsync(orderId, ct);
        var assignments = await db.DispatchAssignments.WithDetails()
            .Where(a => a.OrderId == orderId)
            .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
            .ToListAsync(ct);
        return new OrderDetailDto(MapOrder(order, clock.GetUtcNow()), await assignmentQueries.MapAsync(assignments, ct));
    }

    public async Task<CandidatesDto> GetCandidatesAsync(int orderId, CandidatesQuery q, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var order = await FindOrderAsync(orderId, ct);
        var active = order.Assignments.SingleOrDefault();
        var reassign = q.ExcludeAssignmentId is not null;

        List<RuleViolation> orderViolations;
        if (reassign)
        {
            if (active is null || active.Id != q.ExcludeAssignmentId)
            {
                throw DomainException.Conflict(ErrorCodes.InvalidStatusTransition,
                    $"Assignment {q.ExcludeAssignmentId} is not the active assignment of order {order.OrderNumber}.",
                    BusinessRules.Reassignment);
            }
            orderViolations = [.. evaluator.EvaluateOrderForReassignment(order, now)];
        }
        else
        {
            orderViolations = [.. evaluator.EvaluateOrderForAssignment(order, now)];
            if (active is not null)
            {
                orderViolations.Insert(0, new(BusinessRules.DuplicateAssignment, ErrorCodes.OrderAlreadyAssigned,
                    $"Order {order.OrderNumber} already has active assignment {active.AssignmentNumber}."));
            }
        }

        var drivers = await db.Drivers.AsNoTracking().OrderBy(d => d.DriverCode).ToListAsync(ct);
        var vehicles = await db.Vehicles.AsNoTracking().OrderBy(v => v.VehicleCode).ToListAsync(ct);
        var allActive = await db.DispatchAssignments.AsNoTracking()
            .Include(a => a.Order)
            .Where(a => a.Status == AssignmentStatus.Assigned)
            .ToListAsync(ct);
        var activeForChecks = allActive.Where(a => a.Id != q.ExcludeAssignmentId).ToList();

        Driver? selectedDriver = null;
        if (q.DriverId is { } driverId)
        {
            selectedDriver = drivers.SingleOrDefault(d => d.Id == driverId)
                ?? throw DomainException.NotFound(ErrorCodes.DriverNotFound, $"Driver {driverId} was not found.");
        }
        Vehicle? selectedVehicle = null;
        if (q.VehicleId is { } vehicleId)
        {
            selectedVehicle = vehicles.SingleOrDefault(v => v.Id == vehicleId)
                ?? throw DomainException.NotFound(ErrorCodes.VehicleNotFound, $"Vehicle {vehicleId} was not found.");
        }

        var driverCandidates = drivers.Select(d =>
        {
            List<RuleViolation> reasons =
            [
                .. evaluator.EvaluateDriver(d, order, activeForChecks.Where(a => a.DriverId == d.Id)),
                .. selectedVehicle is null ? [] : evaluator.EvaluateQualification(d, selectedVehicle),
            ];
            var dto = d.ToDto(licenceRules, ResourceSchedule.From(allActive.Where(a => a.DriverId == d.Id), now));
            return new DriverCandidateDto(dto, reasons.Count == 0, reasons);
        })
        .OrderByDescending(c => c.Eligible).ThenBy(c => c.Driver.DriverCode)
        .ToList();

        var vehicleCandidates = vehicles.Select(v =>
        {
            List<RuleViolation> reasons =
            [
                .. evaluator.EvaluateVehicle(v, order, activeForChecks.Where(a => a.VehicleId == v.Id)),
                .. selectedDriver is null ? [] : evaluator.EvaluateQualification(selectedDriver, v),
            ];
            var dto = v.ToDto(licenceRules, ResourceSchedule.From(allActive.Where(a => a.VehicleId == v.Id), now));
            return new VehicleCandidateDto(dto, reasons.Count == 0, reasons);
        })
        .OrderByDescending(c => c.Eligible).ThenBy(c => c.Vehicle.VehicleCode)
        .ToList();

        return new CandidatesDto(MapOrder(order, now), reassign ? "reassign" : "assign", q.ExcludeAssignmentId,
            orderViolations.Count == 0, orderViolations, q.DriverId, q.VehicleId, driverCandidates, vehicleCandidates);
    }

    // ---------------------------------------------------------------------------------------------

    private IQueryable<TransportOrder> OrdersWithActiveAssignment() =>
        db.TransportOrders.AsNoTracking()
            .Include(o => o.Assignments.Where(a => a.Status == AssignmentStatus.Assigned));

    private async Task<TransportOrder> FindOrderAsync(int orderId, CancellationToken ct) =>
        await OrdersWithActiveAssignment().SingleOrDefaultAsync(o => o.Id == orderId, ct)
        ?? throw DomainException.NotFound(ErrorCodes.OrderNotFound, $"Order {orderId} was not found.");

    /// <summary>Expects order.Assignments to contain only its active assignment (if any).</summary>
    private OrderDto MapOrder(TransportOrder order, DateTimeOffset now)
    {
        var active = order.Assignments.SingleOrDefault();
        if (active is not null)
        {
            active.Order = order;
        }
        return order.ToDto(evaluator.EvaluateOrderForAssignment(order, now), active);
    }
}
