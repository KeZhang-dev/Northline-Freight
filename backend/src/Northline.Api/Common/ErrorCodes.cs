namespace Northline.Api.Common;

/// <summary>
/// Stable, machine-readable error codes returned in the "code" field of problem responses
/// and in each rule violation. Documented in docs/API.md.
/// </summary>
public static class ErrorCodes
{
    // Generic
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string InternalError = "INTERNAL_ERROR";

    // Auth
    public const string InvalidCredentials = "INVALID_CREDENTIALS";

    // Not found
    public const string OrderNotFound = "ORDER_NOT_FOUND";
    public const string DriverNotFound = "DRIVER_NOT_FOUND";
    public const string VehicleNotFound = "VEHICLE_NOT_FOUND";
    public const string AssignmentNotFound = "ASSIGNMENT_NOT_FOUND";

    // 422 umbrella code; individual failures are listed in "violations"
    public const string AssignmentValidationFailed = "ASSIGNMENT_VALIDATION_FAILED";

    // BR-01
    public const string OrderNotPending = "ORDER_NOT_PENDING";
    public const string OrderExpired = "ORDER_EXPIRED";
    public const string OrderInvalidTimeWindow = "ORDER_INVALID_TIME_WINDOW";
    // BR-02
    public const string DriverNotActive = "DRIVER_NOT_ACTIVE";
    // BR-03
    public const string VehicleNotActive = "VEHICLE_NOT_ACTIVE";
    // BR-04
    public const string VehicleCapacityExceeded = "VEHICLE_CAPACITY_EXCEEDED";
    // BR-05
    public const string DriverScheduleConflict = "DRIVER_SCHEDULE_CONFLICT";
    // BR-06
    public const string VehicleScheduleConflict = "VEHICLE_SCHEDULE_CONFLICT";
    // BR-07
    public const string DriverLicenceIncompatible = "DRIVER_LICENCE_INCOMPATIBLE";
    // BR-10
    public const string OrderAlreadyAssigned = "ORDER_ALREADY_ASSIGNED";
    // BR-11
    public const string ReassignNoChange = "REASSIGN_NO_CHANGE";

    // Lifecycle / concurrency
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";
    public const string AssignmentModified = "ASSIGNMENT_MODIFIED";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
}

public static class BusinessRules
{
    public const string OrderEligibility = "BR-01";
    public const string DriverAvailability = "BR-02";
    public const string VehicleAvailability = "BR-03";
    public const string VehicleCapacity = "BR-04";
    public const string DriverSchedule = "BR-05";
    public const string VehicleSchedule = "BR-06";
    public const string DriverQualification = "BR-07";
    public const string DuplicateAssignment = "BR-10";
    public const string Reassignment = "BR-11";
    public const string Cancellation = "BR-12";
    public const string Lifecycle = "LIFECYCLE";
}
