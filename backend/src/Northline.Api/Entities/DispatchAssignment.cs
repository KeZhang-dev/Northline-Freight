namespace Northline.Api.Entities;

public class DispatchAssignment
{
    public int Id { get; set; }
    public string AssignmentNumber { get; set; } = string.Empty;

    public int OrderId { get; set; }
    public TransportOrder Order { get; set; } = null!;
    public int DriverId { get; set; }
    public Driver Driver { get; set; } = null!;
    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    /// <summary>Copied from Order.PickupAt when the assignment is created. Window is [StartAt, EndAt).</summary>
    public DateTimeOffset StartAt { get; set; }
    /// <summary>Copied from Order.DeliveryDeadline when the assignment is created.</summary>
    public DateTimeOffset EndAt { get; set; }

    public AssignmentStatus Status { get; set; }

    /// <summary>The assignment this one replaced through reassignment, if any.</summary>
    public int? PreviousAssignmentId { get; set; }
    public DispatchAssignment? PreviousAssignment { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedBy { get; set; } = null!;

    public DateTimeOffset? ClosedAt { get; set; }
    public int? ClosedByUserId { get; set; }
    public User? ClosedBy { get; set; }
    public string? CloseReason { get; set; }

    /// <summary>PostgreSQL xmin system column, used as an optimistic concurrency token.</summary>
    public uint Version { get; set; }
}
