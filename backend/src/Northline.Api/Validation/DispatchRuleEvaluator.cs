using Northline.Api.Common;
using Northline.Api.Entities;

namespace Northline.Api.Validation;

/// <summary>
/// Pure evaluation of the dispatch business rules BR-01 to BR-07. It performs no database access:
/// callers supply the entities and the relevant active assignments, which keeps the rules unit-testable.
/// Every rule is evaluated so that all violations can be reported together.
/// </summary>
public class DispatchRuleEvaluator(LicenceRules licenceRules)
{
    /// <summary>Half-open interval overlap: [aStart, aEnd) and [bStart, bEnd). Back-to-back is not an overlap.</summary>
    public static bool Overlaps(DateTimeOffset aStart, DateTimeOffset aEnd, DateTimeOffset bStart, DateTimeOffset bEnd) =>
        aStart < bEnd && bStart < aEnd;

    public static bool HasValidWindow(TransportOrder order) => order.PickupAt < order.DeliveryDeadline;

    /// <summary>BR-01 for a new assignment: Pending, deadline in the future, pickup before deadline.</summary>
    public IReadOnlyList<RuleViolation> EvaluateOrderForAssignment(TransportOrder order, DateTimeOffset now)
    {
        var violations = new List<RuleViolation>();
        if (order.Status != OrderStatus.Pending)
        {
            violations.Add(new(BusinessRules.OrderEligibility, ErrorCodes.OrderNotPending,
                $"Order {order.OrderNumber} has status {order.Status}. Only Pending orders can be assigned."));
        }
        violations.AddRange(EvaluateOrderTiming(order, now));
        return violations;
    }

    /// <summary>
    /// Order checks for a reassignment. The order is already Assigned, so only the timing rules apply:
    /// an expired job cannot be moved to new resources.
    /// </summary>
    public IReadOnlyList<RuleViolation> EvaluateOrderForReassignment(TransportOrder order, DateTimeOffset now) =>
        EvaluateOrderTiming(order, now).ToList();

    private static IEnumerable<RuleViolation> EvaluateOrderTiming(TransportOrder order, DateTimeOffset now)
    {
        if (!HasValidWindow(order))
        {
            yield return new(BusinessRules.OrderEligibility, ErrorCodes.OrderInvalidTimeWindow,
                $"Order {order.OrderNumber} has an invalid time window: pickup ({NzTime.Format(order.PickupAt)}) " +
                $"must be earlier than the delivery deadline ({NzTime.Format(order.DeliveryDeadline)}).");
        }
        if (order.DeliveryDeadline <= now)
        {
            yield return new(BusinessRules.OrderEligibility, ErrorCodes.OrderExpired,
                $"Order {order.OrderNumber} has expired: its delivery deadline ({NzTime.Format(order.DeliveryDeadline)}) has passed.");
        }
    }

    /// <summary>BR-02 and BR-05.</summary>
    /// <param name="activeAssignments">Active (Assigned) assignments for this driver, excluding any assignment being replaced.</param>
    public IReadOnlyList<RuleViolation> EvaluateDriver(Driver driver, TransportOrder order, IEnumerable<DispatchAssignment> activeAssignments)
    {
        var violations = new List<RuleViolation>();
        if (driver.EmploymentStatus != EmploymentStatus.Active)
        {
            violations.Add(new(BusinessRules.DriverAvailability, ErrorCodes.DriverNotActive,
                $"Driver {driver.DriverCode} ({driver.FullName}) has employment status {driver.EmploymentStatus}. Only Active drivers can be assigned."));
        }
        foreach (var conflict in FindConflicts(order, activeAssignments))
        {
            violations.Add(new(BusinessRules.DriverSchedule, ErrorCodes.DriverScheduleConflict,
                $"Driver {driver.DriverCode} ({driver.FullName}) is already allocated to {conflict.AssignmentNumber} " +
                $"({NzTime.FormatRange(conflict.StartAt, conflict.EndAt)}), which overlaps this job."));
        }
        return violations;
    }

    /// <summary>BR-03, BR-04 and BR-06.</summary>
    /// <param name="activeAssignments">Active (Assigned) assignments for this vehicle, excluding any assignment being replaced.</param>
    public IReadOnlyList<RuleViolation> EvaluateVehicle(Vehicle vehicle, TransportOrder order, IEnumerable<DispatchAssignment> activeAssignments)
    {
        var violations = new List<RuleViolation>();
        if (vehicle.VehicleStatus != VehicleStatus.Active)
        {
            violations.Add(new(BusinessRules.VehicleAvailability, ErrorCodes.VehicleNotActive,
                $"Vehicle {vehicle.VehicleCode} ({vehicle.RegistrationNumber}) has status {vehicle.VehicleStatus}. Only Active vehicles can be assigned."));
        }
        if (vehicle.MaxPayloadKg < order.CargoWeightKg)
        {
            violations.Add(new(BusinessRules.VehicleCapacity, ErrorCodes.VehicleCapacityExceeded,
                $"Vehicle {vehicle.VehicleCode} has a maximum payload of {DisplayNames.Kg(vehicle.MaxPayloadKg)}, " +
                $"which is less than the cargo weight of {DisplayNames.Kg(order.CargoWeightKg)}."));
        }
        foreach (var conflict in FindConflicts(order, activeAssignments))
        {
            violations.Add(new(BusinessRules.VehicleSchedule, ErrorCodes.VehicleScheduleConflict,
                $"Vehicle {vehicle.VehicleCode} ({vehicle.RegistrationNumber}) is already allocated to {conflict.AssignmentNumber} " +
                $"({NzTime.FormatRange(conflict.StartAt, conflict.EndAt)}), which overlaps this job."));
        }
        return violations;
    }

    /// <summary>BR-07.</summary>
    public IReadOnlyList<RuleViolation> EvaluateQualification(Driver driver, Vehicle vehicle)
    {
        if (licenceRules.IsQualified(driver.LicenceClass, vehicle.VehicleType))
        {
            return [];
        }
        return
        [
            new(BusinessRules.DriverQualification, ErrorCodes.DriverLicenceIncompatible,
                $"Driver {driver.DriverCode} holds licence class {driver.LicenceClass}, but a {vehicle.VehicleType.Display()} " +
                $"requires class {licenceRules.MinimumClassFor(vehicle.VehicleType)} or higher.")
        ];
    }

    /// <summary>BR-02 to BR-07 for a driver/vehicle pairing on an order.</summary>
    public IReadOnlyList<RuleViolation> EvaluateResources(
        TransportOrder order, Driver driver, Vehicle vehicle,
        IEnumerable<DispatchAssignment> driverActiveAssignments,
        IEnumerable<DispatchAssignment> vehicleActiveAssignments) =>
    [
        .. EvaluateDriver(driver, order, driverActiveAssignments),
        .. EvaluateVehicle(vehicle, order, vehicleActiveAssignments),
        .. EvaluateQualification(driver, vehicle),
    ];

    private static IEnumerable<DispatchAssignment> FindConflicts(TransportOrder order, IEnumerable<DispatchAssignment> activeAssignments)
    {
        // An order with an invalid window is already rejected by BR-01; there is no meaningful window to compare.
        if (!HasValidWindow(order))
        {
            return [];
        }
        return activeAssignments
            .Where(a => a.Status == AssignmentStatus.Assigned
                        && Overlaps(order.PickupAt, order.DeliveryDeadline, a.StartAt, a.EndAt))
            .OrderBy(a => a.StartAt);
    }
}
