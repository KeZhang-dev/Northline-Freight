namespace Northline.Api.Entities;

public enum UserRole
{
    Admin,
    Dispatcher,
    Driver
}

public enum OrderPriority
{
    Low,
    Normal,
    High,
    Urgent
}

public enum OrderStatus
{
    Pending,
    OnHold,
    Assigned,
    Completed,
    Cancelled
}

public enum EmploymentStatus
{
    Active,
    OnLeave,
    Inactive
}

public enum VehicleType
{
    LightCommercial,
    MediumRigid,
    HeavyRigid,
    HeavyCombination
}

public enum VehicleStatus
{
    Active,
    Maintenance,
    Inactive
}

public enum AssignmentStatus
{
    /// <summary>Active assignment. The only status that occupies driver/vehicle time.</summary>
    Assigned,
    /// <summary>Terminal. Replaced by a newer assignment (see PreviousAssignmentId on the successor).</summary>
    Reassigned,
    /// <summary>Terminal. Cancelled by a dispatcher; the order returns to Pending.</summary>
    Cancelled,
    /// <summary>Terminal. Job finished; the order becomes Completed.</summary>
    Completed
}
