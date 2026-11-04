using Microsoft.EntityFrameworkCore;
using Northline.Api.Common;
using Northline.Api.Data;
using Northline.Api.Dtos;
using Northline.Api.Entities;

namespace Northline.Api.Services;

public interface IAssignmentQueryService
{
    Task<PagedResult<AssignmentDto>> ListAsync(AssignmentListQuery query, CancellationToken ct);
    Task<AssignmentDetailDto> GetAsync(int assignmentId, CancellationToken ct);
    Task<IReadOnlyList<AssignmentDto>> MapAsync(IReadOnlyList<DispatchAssignment> assignments, CancellationToken ct);
}

public class AssignmentQueryService(NorthlineDbContext db, TimeProvider clock) : IAssignmentQueryService
{
    public async Task<PagedResult<AssignmentDto>> ListAsync(AssignmentListQuery q, CancellationToken ct)
    {
        var query = db.DispatchAssignments.WithDetails();

        if (q.Status is { } status) query = query.Where(a => a.Status == status);
        if (q.Priority is { } priority) query = query.Where(a => a.Order.Priority == priority);
        if (q.DriverId is { } driverId) query = query.Where(a => a.DriverId == driverId);
        if (q.VehicleId is { } vehicleId) query = query.Where(a => a.VehicleId == vehicleId);
        if (q.OrderId is { } orderId) query = query.Where(a => a.OrderId == orderId);

        if (QueryHelpers.LikePattern(q.Search) is { } like)
        {
            query = query.Where(a =>
                EF.Functions.ILike(a.AssignmentNumber, like) ||
                EF.Functions.ILike(a.Order.OrderNumber, like) ||
                EF.Functions.ILike(a.Order.CustomerName, like) ||
                EF.Functions.ILike(a.Driver.DriverCode, like) ||
                EF.Functions.ILike(a.Driver.FirstName + " " + a.Driver.LastName, like) ||
                EF.Functions.ILike(a.Vehicle.VehicleCode, like) ||
                EF.Functions.ILike(a.Vehicle.RegistrationNumber, like));
        }

        var ordered = q.Sort switch
        {
            AssignmentSortField.CreatedAt => query.OrderByDir(a => a.CreatedAt, q.Dir),
            AssignmentSortField.AssignmentNumber => query.OrderByDir(a => a.AssignmentNumber, q.Dir),
            AssignmentSortField.Status => query.OrderByDir(a => a.Status, q.Dir),
            AssignmentSortField.Priority => q.Dir == SortDirection.Desc
                ? query.OrderByDescending(a => a.Order.Priority == OrderPriority.Urgent ? 3 : a.Order.Priority == OrderPriority.High ? 2 : a.Order.Priority == OrderPriority.Normal ? 1 : 0)
                : query.OrderBy(a => a.Order.Priority == OrderPriority.Urgent ? 3 : a.Order.Priority == OrderPriority.High ? 2 : a.Order.Priority == OrderPriority.Normal ? 1 : 0),
            AssignmentSortField.OrderNumber => query.OrderByDir(a => a.Order.OrderNumber, q.Dir),
            AssignmentSortField.DriverName => query.OrderByDir(a => a.Driver.FirstName + " " + a.Driver.LastName, q.Dir),
            _ => query.OrderByDir(a => a.StartAt, q.Dir),
        };

        return await ordered.ThenBy(a => a.Id).ToPagedAsync(q, items => MapAsync(items, ct), ct);
    }

    public async Task<AssignmentDetailDto> GetAsync(int assignmentId, CancellationToken ct)
    {
        var assignment = await db.DispatchAssignments.WithDetails().SingleOrDefaultAsync(a => a.Id == assignmentId, ct)
            ?? throw DomainException.NotFound(ErrorCodes.AssignmentNotFound, $"Assignment {assignmentId} was not found.");

        var history = await db.DispatchAssignments.WithDetails()
            .Where(a => a.OrderId == assignment.OrderId)
            .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
            .ToListAsync(ct);

        var mapped = await MapAsync(history, ct);
        return new AssignmentDetailDto(mapped.Single(a => a.Id == assignmentId), mapped);
    }

    public async Task<IReadOnlyList<AssignmentDto>> MapAsync(IReadOnlyList<DispatchAssignment> assignments, CancellationToken ct)
    {
        var ids = assignments.Select(a => a.Id).ToList();
        var successors = await db.DispatchAssignments.AsNoTracking()
            .Where(a => a.PreviousAssignmentId != null && ids.Contains(a.PreviousAssignmentId.Value))
            .ToDictionaryAsync(a => a.PreviousAssignmentId!.Value, ct);

        var now = clock.GetUtcNow();
        return assignments.Select(a => a.ToDto(now, successors.GetValueOrDefault(a.Id))).ToList();
    }
}
