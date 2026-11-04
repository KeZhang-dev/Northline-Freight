using Northline.Api.Entities;

namespace Northline.Api.Dtos;

public enum OrderSortField
{
    PickupAt,
    DeliveryDeadline,
    Priority,
    CargoWeightKg,
    OrderNumber,
    CustomerName
}

public class OrderListQuery : PageQuery
{
    public OrderStatus? Status { get; set; }
    public OrderPriority? Priority { get; set; }

    /// <summary>When true, only orders that satisfy BR-01 right now are returned.</summary>
    public bool? EligibleOnly { get; set; }

    public OrderSortField Sort { get; set; } = OrderSortField.PickupAt;
}

public enum AssignmentSortField
{
    StartAt,
    CreatedAt,
    AssignmentNumber,
    Status,
    Priority,
    OrderNumber,
    DriverName
}

public class AssignmentListQuery : PageQuery
{
    public AssignmentStatus? Status { get; set; }
    public OrderPriority? Priority { get; set; }
    public int? DriverId { get; set; }
    public int? VehicleId { get; set; }
    public int? OrderId { get; set; }
    public AssignmentSortField Sort { get; set; } = AssignmentSortField.StartAt;
}

public class DriverListQuery
{
    public EmploymentStatus? EmploymentStatus { get; set; }
    public bool? AvailableNow { get; set; }
    public string? Search { get; set; }
}

public class VehicleListQuery
{
    public VehicleStatus? VehicleStatus { get; set; }
    public VehicleType? VehicleType { get; set; }
    public bool? AvailableNow { get; set; }
    public string? Search { get; set; }
}

public class CandidatesQuery
{
    /// <summary>Evaluate vehicle licence compatibility (BR-07) against this driver.</summary>
    public int? DriverId { get; set; }

    /// <summary>Evaluate driver licence compatibility (BR-07) against this vehicle.</summary>
    public int? VehicleId { get; set; }

    /// <summary>Reassignment mode: the active assignment being replaced is excluded from conflict checks.</summary>
    public int? ExcludeAssignmentId { get; set; }
}
