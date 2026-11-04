using Northline.Api.Common;
using Northline.Api.Entities;
using Northline.Api.Validation;

namespace Northline.Api.Dtos;

public static class DtoMapper
{
    public const string ActionReassign = "reassign";
    public const string ActionCancel = "cancel";
    public const string ActionComplete = "complete";

    public static UserDto ToDto(this User u) => new(u.Id, u.Email, u.FullName, u.Role, u.DriverId);

    public static UserRefDto ToRef(this User u) => new(u.Id, u.FullName);

    public static AssignmentRefDto ToRef(this DispatchAssignment a) =>
        new(a.Id, a.AssignmentNumber, a.OrderId, a.Order.OrderNumber, a.StartAt, a.EndAt);

    public static OrderRefDto ToRef(this TransportOrder o) =>
        new(o.Id, o.OrderNumber, o.CustomerName, o.PickupLocation, o.DeliveryLocation, o.CargoWeightKg,
            o.PickupAt, o.DeliveryDeadline, o.Priority, o.Status);

    public static DriverRefDto ToRef(this Driver d) => new(d.Id, d.DriverCode, d.FullName, d.LicenceClass, d.EmploymentStatus);

    public static VehicleRefDto ToRef(this Vehicle v) =>
        new(v.Id, v.VehicleCode, v.RegistrationNumber, v.VehicleType, v.MaxPayloadKg, v.VehicleStatus);

    public static OrderDto ToDto(this TransportOrder o, IReadOnlyList<RuleViolation> eligibilityViolations, DispatchAssignment? active) =>
        new(o.Id, o.OrderNumber, o.CustomerName, o.PickupLocation, o.DeliveryLocation, o.CargoDescription,
            o.CargoWeightKg, o.PickupAt, o.DeliveryDeadline, o.Priority, o.Status,
            eligibilityViolations.Count == 0, eligibilityViolations, active?.ToRef(), o.CreatedAt, o.UpdatedAt);

    /// <summary>Requires Order, Driver, Vehicle, CreatedBy, ClosedBy and PreviousAssignment to be loaded.</summary>
    public static AssignmentDto ToDto(this DispatchAssignment a, DateTimeOffset now, DispatchAssignment? replacedBy) =>
        new(a.Id, a.AssignmentNumber, a.Status, a.Version, a.StartAt, a.EndAt,
            a.Order.ToRef(), a.Driver.ToRef(), a.Vehicle.ToRef(),
            a.PreviousAssignmentId, a.PreviousAssignment?.AssignmentNumber,
            replacedBy?.Id, replacedBy?.AssignmentNumber,
            a.Notes, a.CreatedAt, a.CreatedBy.ToRef(), a.ClosedAt, a.ClosedBy?.ToRef(), a.CloseReason,
            AllowedActions(a, now));

    /// <summary>
    /// UI hint only. The backend validates every action independently regardless of this list.
    /// </summary>
    public static IReadOnlyList<string> AllowedActions(DispatchAssignment a, DateTimeOffset now)
    {
        if (a.Status != AssignmentStatus.Assigned)
        {
            return [];
        }
        var actions = new List<string>();
        if (a.Order.DeliveryDeadline > now)
        {
            actions.Add(ActionReassign);
        }
        actions.Add(ActionCancel);
        actions.Add(ActionComplete);
        return actions;
    }

    public static DriverDto ToDto(this Driver d, LicenceRules licenceRules, ResourceSchedule schedule) =>
        new(d.Id, d.DriverCode, d.FirstName, d.LastName, d.FullName, d.LicenceClass,
            licenceRules.PermittedVehicleTypes(d.LicenceClass), d.EmploymentStatus, d.Phone, d.Email,
            d.EmploymentStatus == EmploymentStatus.Active && schedule.Current is null,
            schedule.Current?.ToRef(), schedule.Next?.ToRef(), schedule.UpcomingCount);

    public static VehicleDto ToDto(this Vehicle v, LicenceRules licenceRules, ResourceSchedule schedule) =>
        new(v.Id, v.VehicleCode, v.RegistrationNumber, v.VehicleType, v.MaxPayloadKg,
            licenceRules.MinimumClassFor(v.VehicleType), v.VehicleStatus,
            v.VehicleStatus == VehicleStatus.Active && schedule.Current is null,
            schedule.Current?.ToRef(), schedule.Next?.ToRef(), schedule.UpcomingCount);
}

/// <summary>Derived schedule information for a driver or vehicle, computed from its active assignments.</summary>
public record ResourceSchedule(DispatchAssignment? Current, DispatchAssignment? Next, int UpcomingCount)
{
    public static readonly ResourceSchedule Empty = new(null, null, 0);

    /// <param name="active">Active (Assigned) assignments for one resource, with Order loaded.</param>
    public static ResourceSchedule From(IEnumerable<DispatchAssignment> active, DateTimeOffset now)
    {
        var list = active.Where(a => a.Status == AssignmentStatus.Assigned).ToList();
        var current = list.Where(a => a.StartAt <= now && now < a.EndAt).OrderBy(a => a.StartAt).FirstOrDefault();
        var upcoming = list.Where(a => a.StartAt > now).OrderBy(a => a.StartAt).ToList();
        return new(current, upcoming.FirstOrDefault(), upcoming.Count);
    }
}
