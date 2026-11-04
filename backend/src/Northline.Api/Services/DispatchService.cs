using Microsoft.EntityFrameworkCore;
using Northline.Api.Authorization;
using Northline.Api.Common;
using Northline.Api.Data;
using Northline.Api.Dtos;
using Northline.Api.Entities;
using Northline.Api.Validation;

namespace Northline.Api.Services;

/// <summary>
/// All state-changing dispatch operations. Each operation:
/// <list type="number">
/// <item>runs in a single database transaction;</item>
/// <item>locks rows in a fixed order (order row, then assignment row) to serialise competing requests
///       on the same order/assignment and avoid deadlocks;</item>
/// <item>validates every business rule before writing anything;</item>
/// <item>relies on the database constraints as the final authority for duplicates and overlaps.</item>
/// </list>
/// Any exception before commit rolls the whole transaction back, so no partial changes are left behind (BR-09).
/// </summary>
public interface IDispatchService
{
    Task<AssignmentDto> CreateAsync(CreateAssignmentRequest request, CancellationToken ct);
    Task<ReassignmentResultDto> ReassignAsync(int assignmentId, ReassignAssignmentRequest request, CancellationToken ct);
    Task<AssignmentDto> CancelAsync(int assignmentId, CancelAssignmentRequest request, CancellationToken ct);
    Task<AssignmentDto> CompleteAsync(int assignmentId, CompleteAssignmentRequest request, CancellationToken ct);
}

public class DispatchService(
    NorthlineDbContext db,
    DispatchRuleEvaluator evaluator,
    IAssignmentQueryService assignmentQueries,
    ICurrentUser currentUser,
    TimeProvider clock) : IDispatchService
{
    public async Task<AssignmentDto> CreateAsync(CreateAssignmentRequest request, CancellationToken ct)
    {
        var assignmentId = await InTransactionAsync(async () =>
        {
            var now = clock.GetUtcNow();
            var orderId = request.OrderId!.Value;

            if (!await LockOrderAsync(orderId, ct))
            {
                throw DomainException.NotFound(ErrorCodes.OrderNotFound, $"Order {orderId} was not found.");
            }
            var order = await db.TransportOrders.SingleAsync(o => o.Id == orderId, ct);
            var driver = await LockAndFindDriverAsync(request.DriverId!.Value, ct);
            var vehicle = await LockAndFindVehicleAsync(request.VehicleId!.Value, ct);

            // BR-10: checked after the order lock, so a concurrent request for the same order sees the committed result.
            var existing = await db.DispatchAssignments.AsNoTracking()
                .Where(a => a.OrderId == orderId && a.Status == AssignmentStatus.Assigned)
                .Select(a => a.AssignmentNumber)
                .FirstOrDefaultAsync(ct);
            if (existing is not null)
            {
                throw DomainException.Conflict(ErrorCodes.OrderAlreadyAssigned,
                    $"Order {order.OrderNumber} already has active assignment {existing}. Use reassignment to change its driver or vehicle.",
                    BusinessRules.DuplicateAssignment);
            }

            List<RuleViolation> violations =
            [
                .. evaluator.EvaluateOrderForAssignment(order, now),
                .. evaluator.EvaluateResources(order, driver, vehicle,
                    await ActiveOverlappingAsync(a => a.DriverId == driver.Id, order, null, ct),
                    await ActiveOverlappingAsync(a => a.VehicleId == vehicle.Id, order, null, ct)),
            ];
            if (violations.Count > 0)
            {
                throw DomainException.RuleViolations(violations);
            }

            var assignment = new DispatchAssignment
            {
                AssignmentNumber = await NextAssignmentNumberAsync(ct),
                OrderId = order.Id,
                DriverId = driver.Id,
                VehicleId = vehicle.Id,
                StartAt = order.PickupAt,
                EndAt = order.DeliveryDeadline,
                Status = AssignmentStatus.Assigned,
                Notes = Clean(request.Notes),
                CreatedAt = now,
                CreatedByUserId = currentUser.UserId,
            };
            db.DispatchAssignments.Add(assignment);

            // BR-08: order moves from Pending to Assigned in the same transaction.
            order.Status = OrderStatus.Assigned;
            order.UpdatedAt = now;

            await db.SaveChangesAsync(ct);
            return assignment.Id;
        }, ct);

        return (await assignmentQueries.GetAsync(assignmentId, ct)).Assignment;
    }

    public async Task<ReassignmentResultDto> ReassignAsync(int assignmentId, ReassignAssignmentRequest request, CancellationToken ct)
    {
        var (previousId, currentId) = await InTransactionAsync(async () =>
        {
            var now = clock.GetUtcNow();
            var previous = await LoadForUpdateAsync(assignmentId, request.Version!.Value, "reassigned", ct);
            var order = previous.Order;

            var newDriverId = request.DriverId ?? previous.DriverId;
            var newVehicleId = request.VehicleId ?? previous.VehicleId;
            if (newDriverId == previous.DriverId && newVehicleId == previous.VehicleId)
            {
                throw DomainException.RuleViolations(
                [
                    new(BusinessRules.Reassignment, ErrorCodes.ReassignNoChange,
                        $"Reassignment of {previous.AssignmentNumber} must change the driver, the vehicle, or both.")
                ]);
            }

            var driver = await LockAndFindDriverAsync(newDriverId, ct);
            var vehicle = await LockAndFindVehicleAsync(newVehicleId, ct);

            // The assignment being replaced is excluded from the conflict checks.
            List<RuleViolation> violations =
            [
                .. evaluator.EvaluateOrderForReassignment(order, now),
                .. evaluator.EvaluateResources(order, driver, vehicle,
                    await ActiveOverlappingAsync(a => a.DriverId == driver.Id, order, previous.Id, ct),
                    await ActiveOverlappingAsync(a => a.VehicleId == vehicle.Id, order, previous.Id, ct)),
            ];
            if (violations.Count > 0)
            {
                throw DomainException.RuleViolations(violations);
            }

            // Close the old record first and flush it, so it no longer takes part in the
            // active-assignment constraints when the replacement is inserted.
            Close(previous, AssignmentStatus.Reassigned, Clean(request.Reason), now);
            order.UpdatedAt = now;
            await db.SaveChangesAsync(ct);

            var current = new DispatchAssignment
            {
                AssignmentNumber = await NextAssignmentNumberAsync(ct),
                OrderId = order.Id,
                DriverId = driver.Id,
                VehicleId = vehicle.Id,
                StartAt = order.PickupAt,
                EndAt = order.DeliveryDeadline,
                Status = AssignmentStatus.Assigned,
                PreviousAssignmentId = previous.Id,
                Notes = previous.Notes,
                CreatedAt = now,
                CreatedByUserId = currentUser.UserId,
            };
            db.DispatchAssignments.Add(current);
            await db.SaveChangesAsync(ct);

            return (previous.Id, current.Id);
        }, ct);

        var previousDto = (await assignmentQueries.GetAsync(previousId, ct)).Assignment;
        var currentDto = (await assignmentQueries.GetAsync(currentId, ct)).Assignment;
        return new ReassignmentResultDto(previousDto, currentDto);
    }

    public async Task<AssignmentDto> CancelAsync(int assignmentId, CancelAssignmentRequest request, CancellationToken ct)
    {
        await InTransactionAsync(async () =>
        {
            var now = clock.GetUtcNow();
            var assignment = await LoadForUpdateAsync(assignmentId, request.Version!.Value, "cancelled", ct);

            // BR-12: closing the record releases the driver/vehicle window, because only
            // 'Assigned' rows take part in conflict checks and exclusion constraints.
            Close(assignment, AssignmentStatus.Cancelled, Clean(request.Reason), now);
            assignment.Order.Status = OrderStatus.Pending;
            assignment.Order.UpdatedAt = now;

            await db.SaveChangesAsync(ct);
            return assignment.Id;
        }, ct);

        return (await assignmentQueries.GetAsync(assignmentId, ct)).Assignment;
    }

    public async Task<AssignmentDto> CompleteAsync(int assignmentId, CompleteAssignmentRequest request, CancellationToken ct)
    {
        await InTransactionAsync(async () =>
        {
            var now = clock.GetUtcNow();
            var assignment = await LoadForUpdateAsync(assignmentId, request.Version!.Value, "completed", ct);

            Close(assignment, AssignmentStatus.Completed, Clean(request.Reason), now);
            assignment.Order.Status = OrderStatus.Completed;
            assignment.Order.UpdatedAt = now;

            await db.SaveChangesAsync(ct);
            return assignment.Id;
        }, ct);

        return (await assignmentQueries.GetAsync(assignmentId, ct)).Assignment;
    }

    // ---------------------------------------------------------------------------------------------

    private void Close(DispatchAssignment assignment, AssignmentStatus status, string? reason, DateTimeOffset now)
    {
        assignment.Status = status;
        assignment.ClosedAt = now;
        assignment.ClosedByUserId = currentUser.UserId;
        assignment.CloseReason = reason;
    }

    /// <summary>
    /// Locks the assignment's order and the assignment itself, then checks the client's version and the
    /// current status. Check order: 404, 409 ASSIGNMENT_MODIFIED, 409 INVALID_STATUS_TRANSITION.
    /// </summary>
    private async Task<DispatchAssignment> LoadForUpdateAsync(int assignmentId, uint expectedVersion, string verb, CancellationToken ct)
    {
        var orderId = await db.DispatchAssignments.AsNoTracking()
            .Where(a => a.Id == assignmentId)
            .Select(a => (int?)a.OrderId)
            .SingleOrDefaultAsync(ct);
        if (orderId is null)
        {
            throw DomainException.NotFound(ErrorCodes.AssignmentNotFound, $"Assignment {assignmentId} was not found.");
        }

        await LockOrderAsync(orderId.Value, ct);
        await db.Database.SqlQuery<int>(
                $"SELECT id AS \"Value\" FROM dispatch_assignments WHERE id = {assignmentId} FOR UPDATE")
            .ToListAsync(ct);

        // Loaded after the locks, so this reflects the latest committed state.
        var assignment = await db.DispatchAssignments
            .Include(a => a.Order)
            .SingleAsync(a => a.Id == assignmentId, ct);

        if (assignment.Version != expectedVersion)
        {
            throw DomainException.Conflict(ErrorCodes.AssignmentModified,
                $"Assignment {assignment.AssignmentNumber} has been modified since you loaded it " +
                $"(current status: {assignment.Status}). Reload it and try again.",
                BusinessRules.Lifecycle);
        }
        if (assignment.Status != AssignmentStatus.Assigned)
        {
            throw DomainException.Conflict(ErrorCodes.InvalidStatusTransition,
                $"Assignment {assignment.AssignmentNumber} has status {assignment.Status} and cannot be {verb}. " +
                "Only Assigned assignments can be changed.",
                BusinessRules.Lifecycle);
        }
        return assignment;
    }

    /// <summary>SELECT ... FOR UPDATE on the order row. Returns false if the order does not exist.</summary>
    private async Task<bool> LockOrderAsync(int orderId, CancellationToken ct)
    {
        var rows = await db.Database.SqlQuery<int>(
                $"SELECT id AS \"Value\" FROM transport_orders WHERE id = {orderId} FOR UPDATE")
            .ToListAsync(ct);
        return rows.Count > 0;
    }

    // Driver and vehicle rows are locked (driver first, then vehicle) before their schedules are checked.
    // This serialises allocation of the same resource: a competing request waits, then sees the committed
    // assignment and fails with a precise BR-05/BR-06 message. Without it, two concurrent inserts can
    // deadlock inside the exclusion constraint. FOR NO KEY UPDATE does not block foreign-key checks.

    private async Task<Driver> LockAndFindDriverAsync(int driverId, CancellationToken ct)
    {
        await db.Database.SqlQuery<int>(
                $"SELECT id AS \"Value\" FROM drivers WHERE id = {driverId} FOR NO KEY UPDATE")
            .ToListAsync(ct);
        return await db.Drivers.AsNoTracking().SingleOrDefaultAsync(d => d.Id == driverId, ct)
            ?? throw DomainException.NotFound(ErrorCodes.DriverNotFound, $"Driver {driverId} was not found.");
    }

    private async Task<Vehicle> LockAndFindVehicleAsync(int vehicleId, CancellationToken ct)
    {
        await db.Database.SqlQuery<int>(
                $"SELECT id AS \"Value\" FROM vehicles WHERE id = {vehicleId} FOR NO KEY UPDATE")
            .ToListAsync(ct);
        return await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw DomainException.NotFound(ErrorCodes.VehicleNotFound, $"Vehicle {vehicleId} was not found.");
    }

    private async Task<List<DispatchAssignment>> ActiveOverlappingAsync(
        System.Linq.Expressions.Expression<Func<DispatchAssignment, bool>> resourceFilter,
        TransportOrder order, int? excludeAssignmentId, CancellationToken ct)
    {
        if (!DispatchRuleEvaluator.HasValidWindow(order))
        {
            return [];
        }
        return await db.DispatchAssignments.AsNoTracking()
            .Where(resourceFilter)
            .Where(a => a.Status == AssignmentStatus.Assigned
                        && a.Id != excludeAssignmentId
                        && a.StartAt < order.DeliveryDeadline
                        && order.PickupAt < a.EndAt)
            .ToListAsync(ct);
    }

    private async Task<string> NextAssignmentNumberAsync(CancellationToken ct)
    {
        var next = await db.Database
            .SqlQuery<long>($"SELECT nextval('assignment_number_seq') AS \"Value\"")
            .SingleAsync(ct);
        return $"DA-{next:D4}";
    }

    private async Task<T> InTransactionAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var result = await operation();
            await tx.CommitAsync(ct);
            return result;
        }
        catch (Exception ex) when (ex is not DomainException && PostgresErrorTranslator.Translate(ex) is { } domain)
        {
            // The transaction has already been rolled back by disposal.
            throw domain;
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
