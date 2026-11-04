namespace Northline.Api.Entities;

public class TransportOrder
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string PickupLocation { get; set; } = string.Empty;
    public string DeliveryLocation { get; set; } = string.Empty;
    public string CargoDescription { get; set; } = string.Empty;
    public decimal CargoWeightKg { get; set; }
    public DateTimeOffset PickupAt { get; set; }
    public DateTimeOffset DeliveryDeadline { get; set; }
    public OrderPriority Priority { get; set; }
    public OrderStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>PostgreSQL xmin system column, used as an optimistic concurrency token.</summary>
    public uint Version { get; set; }

    public List<DispatchAssignment> Assignments { get; set; } = [];
}
