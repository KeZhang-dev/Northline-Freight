using Microsoft.Extensions.Options;
using Northline.Api.Common;
using Northline.Api.Entities;
using Northline.Api.Validation;

namespace Northline.Api.Tests.Unit;

public class DispatchRuleEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly DispatchRuleEvaluator _evaluator = new(new LicenceRules(Options.Create(new DispatchOptions())));

    private static TransportOrder Order(
        OrderStatus status = OrderStatus.Pending, decimal kg = 1_000, int startHours = 24, int endHours = 28) => new()
    {
        Id = 1, OrderNumber = "TO-T1", Status = status, CargoWeightKg = kg,
        PickupAt = Now.AddHours(startHours), DeliveryDeadline = Now.AddHours(endHours),
    };

    private static Driver Driver(int cls = 5, EmploymentStatus status = EmploymentStatus.Active) =>
        new() { Id = 1, DriverCode = "DRV-T1", FirstName = "Test", LastName = "Driver", LicenceClass = cls, EmploymentStatus = status };

    private static Vehicle Vehicle(VehicleType type = VehicleType.HeavyRigid, decimal payload = 12_000, VehicleStatus status = VehicleStatus.Active) =>
        new() { Id = 1, VehicleCode = "VEH-T1", RegistrationNumber = "TST001", VehicleType = type, MaxPayloadKg = payload, VehicleStatus = status };

    private static DispatchAssignment Active(int startHours, int endHours, AssignmentStatus status = AssignmentStatus.Assigned) => new()
    {
        Id = 99, AssignmentNumber = "DA-T99", Status = status, StartAt = Now.AddHours(startHours), EndAt = Now.AddHours(endHours),
    };

    private static IEnumerable<string> Codes(IEnumerable<RuleViolation> violations) => violations.Select(v => v.Code);

    // ---------- BR-01 ----------

    [Fact]
    public void Pending_future_order_with_valid_window_is_eligible() =>
        Assert.Empty(_evaluator.EvaluateOrderForAssignment(Order(), Now));

    [Theory]
    [InlineData(OrderStatus.OnHold)]
    [InlineData(OrderStatus.Assigned)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    public void Non_pending_order_is_rejected(OrderStatus status) =>
        Assert.Equal([ErrorCodes.OrderNotPending], Codes(_evaluator.EvaluateOrderForAssignment(Order(status), Now)));

    [Fact]
    public void Order_with_deadline_in_past_is_expired() =>
        Assert.Contains(ErrorCodes.OrderExpired, Codes(_evaluator.EvaluateOrderForAssignment(Order(startHours: -5, endHours: -1), Now)));

    [Fact]
    public void Order_with_deadline_exactly_now_is_expired() =>
        Assert.Contains(ErrorCodes.OrderExpired, Codes(_evaluator.EvaluateOrderForAssignment(Order(startHours: -5, endHours: 0), Now)));

    [Fact]
    public void Order_whose_pickup_has_passed_but_deadline_has_not_is_eligible() =>
        Assert.Empty(_evaluator.EvaluateOrderForAssignment(Order(startHours: -1, endHours: 3), Now));

    [Theory]
    [InlineData(30, 28)]
    [InlineData(28, 28)]
    public void Order_with_pickup_not_before_deadline_has_invalid_window(int start, int end) =>
        Assert.Contains(ErrorCodes.OrderInvalidTimeWindow, Codes(_evaluator.EvaluateOrderForAssignment(Order(startHours: start, endHours: end), Now)));

    [Fact]
    public void Reassignment_does_not_require_pending_status() =>
        Assert.Empty(_evaluator.EvaluateOrderForReassignment(Order(OrderStatus.Assigned), Now));

    [Fact]
    public void Reassignment_of_expired_order_is_rejected() =>
        Assert.Equal([ErrorCodes.OrderExpired],
            Codes(_evaluator.EvaluateOrderForReassignment(Order(OrderStatus.Assigned, startHours: -5, endHours: -1), Now)));

    // ---------- BR-02 / BR-03 ----------

    [Theory]
    [InlineData(EmploymentStatus.OnLeave)]
    [InlineData(EmploymentStatus.Inactive)]
    public void Non_active_driver_is_rejected(EmploymentStatus status) =>
        Assert.Equal([ErrorCodes.DriverNotActive], Codes(_evaluator.EvaluateDriver(Driver(status: status), Order(), [])));

    [Theory]
    [InlineData(VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.Inactive)]
    public void Non_active_vehicle_is_rejected(VehicleStatus status) =>
        Assert.Equal([ErrorCodes.VehicleNotActive], Codes(_evaluator.EvaluateVehicle(Vehicle(status: status), Order(), [])));

    // ---------- BR-04 ----------

    [Fact]
    public void Payload_equal_to_cargo_weight_is_sufficient() =>
        Assert.Empty(_evaluator.EvaluateVehicle(Vehicle(payload: 5_000), Order(kg: 5_000), []));

    [Fact]
    public void Payload_below_cargo_weight_is_rejected() =>
        Assert.Equal([ErrorCodes.VehicleCapacityExceeded], Codes(_evaluator.EvaluateVehicle(Vehicle(payload: 4_999.99m), Order(kg: 5_000), [])));

    // ---------- BR-05 / BR-06 ----------

    [Theory]
    [InlineData(20, 25)]   // overlaps start
    [InlineData(27, 30)]   // overlaps end
    [InlineData(25, 26)]   // inside
    [InlineData(20, 30)]   // encloses
    [InlineData(24, 28)]   // identical
    public void Overlapping_active_assignment_is_a_conflict(int start, int end)
    {
        Assert.Equal([ErrorCodes.DriverScheduleConflict], Codes(_evaluator.EvaluateDriver(Driver(), Order(), [Active(start, end)])));
        Assert.Equal([ErrorCodes.VehicleScheduleConflict], Codes(_evaluator.EvaluateVehicle(Vehicle(), Order(), [Active(start, end)])));
    }

    [Theory]
    [InlineData(20, 24)]   // ends exactly at pickup
    [InlineData(28, 32)]   // starts exactly at deadline
    [InlineData(1, 5)]
    public void Back_to_back_or_separate_assignment_is_not_a_conflict(int start, int end) =>
        Assert.Empty(_evaluator.EvaluateDriver(Driver(), Order(), [Active(start, end)]));

    [Theory]
    [InlineData(AssignmentStatus.Cancelled)]
    [InlineData(AssignmentStatus.Reassigned)]
    [InlineData(AssignmentStatus.Completed)]
    public void Closed_assignments_do_not_block(AssignmentStatus status) =>
        Assert.Empty(_evaluator.EvaluateVehicle(Vehicle(), Order(), [Active(24, 28, status)]));

    [Fact]
    public void Schedule_checks_are_skipped_when_order_window_is_invalid() =>
        Assert.Empty(_evaluator.EvaluateDriver(Driver(), Order(startHours: 30, endHours: 28), [Active(27, 31)]));

    // ---------- BR-07 ----------

    [Theory]
    [InlineData(1, VehicleType.LightCommercial, true)]
    [InlineData(1, VehicleType.MediumRigid, false)]
    [InlineData(2, VehicleType.MediumRigid, true)]
    [InlineData(3, VehicleType.HeavyRigid, false)]
    [InlineData(4, VehicleType.HeavyRigid, true)]
    [InlineData(4, VehicleType.HeavyCombination, false)]
    [InlineData(5, VehicleType.HeavyCombination, true)]
    [InlineData(5, VehicleType.LightCommercial, true)]
    public void Licence_class_compatibility(int licenceClass, VehicleType type, bool qualified)
    {
        var violations = _evaluator.EvaluateQualification(Driver(licenceClass), Vehicle(type));
        Assert.Equal(qualified, violations.Count == 0);
    }

    [Fact]
    public void Licence_rules_are_configurable()
    {
        var options = new DispatchOptions();
        options.MinimumLicenceClass[VehicleType.MediumRigid] = 3;
        var evaluator = new DispatchRuleEvaluator(new LicenceRules(Options.Create(options)));

        Assert.NotEmpty(evaluator.EvaluateQualification(Driver(2), Vehicle(VehicleType.MediumRigid)));
    }

    [Fact]
    public void Options_validator_rejects_missing_or_out_of_range_entries()
    {
        var options = new DispatchOptions();
        options.MinimumLicenceClass.Remove(VehicleType.HeavyRigid);
        options.MinimumLicenceClass[VehicleType.LightCommercial] = 6;

        var result = new DispatchOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Equal(2, result.Failures!.Count());
    }

    // ---------- Combined ----------

    [Fact]
    public void All_resource_violations_are_reported_together()
    {
        var violations = _evaluator.EvaluateResources(Order(kg: 20_000),
            Driver(1, EmploymentStatus.Inactive), Vehicle(VehicleType.HeavyRigid, 12_000, VehicleStatus.Maintenance),
            [Active(25, 26)], [Active(25, 26)]);

        Assert.Equal(
            [ErrorCodes.DriverNotActive, ErrorCodes.DriverScheduleConflict, ErrorCodes.VehicleNotActive,
             ErrorCodes.VehicleCapacityExceeded, ErrorCodes.VehicleScheduleConflict, ErrorCodes.DriverLicenceIncompatible],
            Codes(violations));
        Assert.Equal(["BR-02", "BR-05", "BR-03", "BR-04", "BR-06", "BR-07"], violations.Select(v => v.Rule));
    }
}
