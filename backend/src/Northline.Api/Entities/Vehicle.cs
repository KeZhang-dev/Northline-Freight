namespace Northline.Api.Entities;

public class Vehicle
{
    public int Id { get; set; }
    public string VehicleCode { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; }
    public decimal MaxPayloadKg { get; set; }
    public VehicleStatus VehicleStatus { get; set; }

    public List<DispatchAssignment> Assignments { get; set; } = [];
}
