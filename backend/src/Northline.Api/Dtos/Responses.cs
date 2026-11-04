using Northline.Api.Common;
using Northline.Api.Entities;

namespace Northline.Api.Dtos;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

// ---------- Auth ----------

public record UserDto(int Id, string Email, string FullName, UserRole Role, int? DriverId);

public record LoginResponse(string Token, DateTimeOffset ExpiresAt, UserDto User);

// ---------- References (compact nested objects) ----------

public record UserRefDto(int Id, string FullName);

public record AssignmentRefDto(int Id, string AssignmentNumber, int OrderId, string OrderNumber, DateTimeOffset StartAt, DateTimeOffset EndAt);

public record OrderRefDto(
    int Id, string OrderNumber, string CustomerName, string PickupLocation, string DeliveryLocation,
    decimal CargoWeightKg, DateTimeOffset PickupAt, DateTimeOffset DeliveryDeadline, OrderPriority Priority, OrderStatus Status);

public record DriverRefDto(int Id, string DriverCode, string FullName, int LicenceClass, EmploymentStatus EmploymentStatus);

public record VehicleRefDto(int Id, string VehicleCode, string RegistrationNumber, VehicleType VehicleType, decimal MaxPayloadKg, VehicleStatus VehicleStatus);

// ---------- Orders ----------

public record OrderDto(
    int Id,
    string OrderNumber,
    string CustomerName,
    string PickupLocation,
    string DeliveryLocation,
    string CargoDescription,
    decimal CargoWeightKg,
    DateTimeOffset PickupAt,
    DateTimeOffset DeliveryDeadline,
    OrderPriority Priority,
    OrderStatus Status,
    bool IsEligible,
    IReadOnlyList<RuleViolation> IneligibilityReasons,
    AssignmentRefDto? ActiveAssignment,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record OrderDetailDto(OrderDto Order, IReadOnlyList<AssignmentDto> Assignments);

// ---------- Resources ----------

public record DriverDto(
    int Id,
    string DriverCode,
    string FirstName,
    string LastName,
    string FullName,
    int LicenceClass,
    IReadOnlyList<VehicleType> PermittedVehicleTypes,
    EmploymentStatus EmploymentStatus,
    string Phone,
    string Email,
    bool IsAvailableNow,
    AssignmentRefDto? CurrentAssignment,
    AssignmentRefDto? NextAssignment,
    int UpcomingAssignmentCount);

public record VehicleDto(
    int Id,
    string VehicleCode,
    string RegistrationNumber,
    VehicleType VehicleType,
    decimal MaxPayloadKg,
    int MinimumLicenceClass,
    VehicleStatus VehicleStatus,
    bool IsAvailableNow,
    AssignmentRefDto? CurrentAssignment,
    AssignmentRefDto? NextAssignment,
    int UpcomingAssignmentCount);

public record DriverDetailDto(DriverDto Driver, IReadOnlyList<AssignmentDto> Assignments);

public record VehicleDetailDto(VehicleDto Vehicle, IReadOnlyList<AssignmentDto> Assignments);

// ---------- Assignments ----------

public record AssignmentDto(
    int Id,
    string AssignmentNumber,
    AssignmentStatus Status,
    uint Version,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    OrderRefDto Order,
    DriverRefDto Driver,
    VehicleRefDto Vehicle,
    int? PreviousAssignmentId,
    string? PreviousAssignmentNumber,
    int? ReplacedByAssignmentId,
    string? ReplacedByAssignmentNumber,
    string? Notes,
    DateTimeOffset CreatedAt,
    UserRefDto CreatedBy,
    DateTimeOffset? ClosedAt,
    UserRefDto? ClosedBy,
    string? CloseReason,
    IReadOnlyList<string> AllowedActions);

/// <param name="History">Every assignment ever made for the same order, oldest first (includes this one).</param>
public record AssignmentDetailDto(AssignmentDto Assignment, IReadOnlyList<AssignmentDto> History);

/// <summary>Result of a reassignment: the closed previous record and the new active record.</summary>
public record ReassignmentResultDto(AssignmentDto Previous, AssignmentDto Current);

// ---------- Candidates ----------

public record DriverCandidateDto(DriverDto Driver, bool Eligible, IReadOnlyList<RuleViolation> Reasons);

public record VehicleCandidateDto(VehicleDto Vehicle, bool Eligible, IReadOnlyList<RuleViolation> Reasons);

/// <param name="Mode">"assign" for a new assignment, "reassign" when excludeAssignmentId is supplied.</param>
public record CandidatesDto(
    OrderDto Order,
    string Mode,
    int? ExcludedAssignmentId,
    bool OrderEligible,
    IReadOnlyList<RuleViolation> OrderViolations,
    int? SelectedDriverId,
    int? SelectedVehicleId,
    IReadOnlyList<DriverCandidateDto> Drivers,
    IReadOnlyList<VehicleCandidateDto> Vehicles);

// ---------- Summary / dashboard ----------

public record DispatchSummaryDto(
    DateTimeOffset AsOf,
    int PendingOrders,
    int EligiblePendingOrders,
    int UrgentPendingOrders,
    int AssignedJobs,
    int AvailableDrivers,
    int ActiveDrivers,
    int TotalDrivers,
    int AvailableVehicles,
    int ActiveVehicles,
    int TotalVehicles);

public record CountByLabel(string Label, int Count);

public record DashboardStatsDto(
    DateTimeOffset AsOf,
    string DataNotice,
    IReadOnlyList<CountByLabel> OrdersByStatus,
    IReadOnlyList<CountByLabel> AssignmentsByStatus,
    IReadOnlyList<CountByLabel> DriversByEmploymentStatus,
    IReadOnlyList<CountByLabel> VehiclesByStatus,
    int JobsScheduledNext7Days);

public record DriverProfileDto(UserDto User, DriverRefDto? Driver, string Message);

public record ResetResultDto(DateTimeOffset ResetAt, int Orders, int Drivers, int Vehicles, int Assignments, int Users);
