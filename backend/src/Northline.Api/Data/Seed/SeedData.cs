using Microsoft.AspNetCore.Identity;
using Northline.Api.Common;
using Northline.Api.Entities;

namespace Northline.Api.Data.Seed;

/// <summary>
/// Deterministic development dataset. All dates are relative to the moment of seeding, in
/// New Zealand local time (Pacific/Auckland), so the data never goes stale.
/// "D" = the seeding day; "D+1 08:00" = tomorrow at 08:00 Auckland time.
///
/// Every record and the scenario it exists for is documented in docs/SEED_DATA.md.
/// Keep that document in sync with this file.
/// </summary>
public static class SeedData
{
    public const int SeededAssignmentCount = 7;

    public record Dataset(
        List<User> Users,
        List<Driver> Drivers,
        List<Vehicle> Vehicles,
        List<TransportOrder> Orders,
        List<DispatchAssignment> Assignments);

    public static Dataset Build(DateTimeOffset nowUtc, string devPassword, IPasswordHasher<User> hasher)
    {
        var today = NzTime.LocalToday(nowUtc);
        DateTimeOffset At(int dayOffset, int hour, int minute = 0) =>
            NzTime.FromLocal(today.AddDays(dayOffset).AddHours(hour).AddMinutes(minute));

        // Relative to "now" rather than a day boundary: an in-progress job that started 2 hours ago.
        var nowRounded = new DateTimeOffset(nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, nowUtc.Minute / 15 * 15, 0, TimeSpan.Zero);
        var inProgressStart = nowRounded.AddHours(-2);
        var inProgressEnd = nowRounded.AddHours(4);

        // ---------------- Drivers ----------------
        Driver D(string code, string first, string last, int cls, EmploymentStatus status, string phone) => new()
        {
            DriverCode = code, FirstName = first, LastName = last, LicenceClass = cls, EmploymentStatus = status,
            Phone = phone, Email = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant().Replace("'", "")}@northline.test",
        };
        var drivers = new List<Driver>
        {
            D("DRV-001", "Aroha", "Ngata", 5, EmploymentStatus.Active, "021 555 0101"),
            D("DRV-002", "James", "Wilson", 5, EmploymentStatus.Active, "021 555 0102"),
            D("DRV-003", "Priya", "Sharma", 4, EmploymentStatus.Active, "021 555 0103"),
            D("DRV-004", "Liam", "O'Connor", 2, EmploymentStatus.Active, "021 555 0104"),
            D("DRV-005", "Mere", "Tawhiri", 1, EmploymentStatus.Active, "021 555 0105"),
            D("DRV-006", "Daniel", "Chen", 5, EmploymentStatus.OnLeave, "021 555 0106"),
            D("DRV-007", "Sione", "Taufa", 4, EmploymentStatus.Inactive, "021 555 0107"),
            D("DRV-008", "Emma", "Clarke", 2, EmploymentStatus.Active, "021 555 0108"),
            D("DRV-009", "Hemi", "Walker", 5, EmploymentStatus.Active, "021 555 0109"),
            D("DRV-010", "Sophie", "Martin", 1, EmploymentStatus.Active, "021 555 0110"),
        };
        Driver Drv(string code) => drivers.Single(d => d.DriverCode == code);

        // ---------------- Vehicles ----------------
        Vehicle V(string code, string rego, VehicleType type, decimal payload, VehicleStatus status) => new()
        {
            VehicleCode = code, RegistrationNumber = rego, VehicleType = type, MaxPayloadKg = payload, VehicleStatus = status,
        };
        var vehicles = new List<Vehicle>
        {
            V("VEH-01", "KXR482", VehicleType.LightCommercial, 1_200, VehicleStatus.Active),
            V("VEH-02", "LTM205", VehicleType.LightCommercial, 1_500, VehicleStatus.Active),
            V("VEH-03", "HJW771", VehicleType.MediumRigid, 6_000, VehicleStatus.Active),
            V("VEH-04", "MBD319", VehicleType.MediumRigid, 8_000, VehicleStatus.Maintenance),
            V("VEH-05", "JRT604", VehicleType.HeavyRigid, 12_000, VehicleStatus.Active),
            V("VEH-06", "PKE118", VehicleType.HeavyRigid, 14_000, VehicleStatus.Active),
            V("VEH-07", "NZH952", VehicleType.HeavyCombination, 25_000, VehicleStatus.Active),
            V("VEH-08", "GHT427", VehicleType.HeavyCombination, 28_000, VehicleStatus.Inactive),
            V("VEH-09", "RWA830", VehicleType.HeavyRigid, 12_000, VehicleStatus.Active),
            V("VEH-10", "CKL566", VehicleType.MediumRigid, 7_000, VehicleStatus.Active),
        };
        Vehicle Veh(string code) => vehicles.Single(v => v.VehicleCode == code);

        // ---------------- Users ----------------
        User U(string email, string name, UserRole role, Driver? driver = null)
        {
            var user = new User { Email = email, FullName = name, Role = role, Driver = driver, IsActive = true };
            user.PasswordHash = hasher.HashPassword(user, devPassword);
            return user;
        }
        var admin = U("admin@northline.test", "Alex Morgan", UserRole.Admin);
        var dispatcher = U("dispatcher@northline.test", "Rachel Thompson", UserRole.Dispatcher);
        var dispatcher2 = U("dispatcher2@northline.test", "Tama Rangi", UserRole.Dispatcher);
        var driverUser = U("driver@northline.test", "Mere Tawhiri", UserRole.Driver, Drv("DRV-005"));
        var users = new List<User> { admin, dispatcher, dispatcher2, driverUser };

        // ---------------- Orders ----------------
        var created = nowUtc.AddDays(-3);
        TransportOrder O(string number, string customer, string pickup, string delivery, string cargo, decimal kg,
            DateTimeOffset pickupAt, DateTimeOffset deadline, OrderPriority priority, OrderStatus status) => new()
        {
            OrderNumber = number, CustomerName = customer, PickupLocation = pickup, DeliveryLocation = delivery,
            CargoDescription = cargo, CargoWeightKg = kg, PickupAt = pickupAt, DeliveryDeadline = deadline,
            Priority = priority, Status = status, CreatedAt = created, UpdatedAt = created,
        };
        var orders = new List<TransportOrder>
        {
            // Happy path: small load, any active driver/vehicle pairing works.
            O("TO-1001", "Harbourside Retail Group", "Penrose, Auckland", "Wiri, Manukau",
                "Boxed retail stock, 4 pallets", 900, At(1, 8), At(1, 12), OrderPriority.High, OrderStatus.Pending),
            // Capacity: only VEH-07 (Heavy Combination) can carry it, which requires a class 5 driver.
            O("TO-1002", "Southern Cross Beverages", "East Tamaki, Auckland", "Mount Maunganui, Tauranga",
                "Bottled beverages, 20 pallets", 22_000, At(1, 7), At(1, 15), OrderPriority.Urgent, OrderStatus.Pending),
            // Overlap: window sits inside DA-0001 (DRV-002 / VEH-05).
            O("TO-1003", "Pokeno Dairy Co-operative", "Pokeno", "Te Rapa, Hamilton",
                "Chilled dairy products", 10_000, At(1, 10), At(1, 13), OrderPriority.Normal, OrderStatus.Pending),
            // Licence: Medium Rigid load; class 1 drivers (DRV-005, DRV-010) are not qualified.
            O("TO-1004", "Waikato Building Supplies", "Te Rapa, Hamilton", "Cambridge",
                "Timber framing and plasterboard", 5_000, At(2, 9), At(2, 13), OrderPriority.Low, OrderStatus.Pending),
            // Capacity: heavier than every vehicle in the fleet.
            O("TO-1005", "Northern Steel Fabricators", "Wiri, Manukau", "Te Rapa, Hamilton",
                "Structural steel beams", 30_000, At(2, 6), At(2, 14), OrderPriority.High, OrderStatus.Pending),
            // Expired: Pending but the deadline has passed.
            O("TO-1006", "Tamaki Pharmaceuticals", "East Tamaki, Auckland", "Te Rapa, Hamilton",
                "Pharmaceutical supplies (temperature controlled)", 1_100, At(-1, 8), At(-1, 12), OrderPriority.Urgent, OrderStatus.Pending),
            // On hold: not eligible.
            O("TO-1007", "Greenlane Office Furniture", "Auckland CBD", "Mount Maunganui, Tauranga",
                "Office furniture", 3_500, At(2, 8), At(2, 16), OrderPriority.Normal, OrderStatus.OnHold),
            // Invalid time window: deadline is before pickup (data-entry error).
            O("TO-1008", "Manukau Electrical Wholesale", "Wiri, Manukau", "Pokeno",
                "Electrical cable drums", 2_400, At(3, 15), At(3, 9), OrderPriority.Normal, OrderStatus.Pending),
            // Assigned via DA-0001 - duplicate assignment target.
            O("TO-1009", "Kauri Coast Foods", "Penrose, Auckland", "Te Rapa, Hamilton",
                "Packaged dry goods", 9_500, At(1, 6), At(1, 14), OrderPriority.High, OrderStatus.Assigned),
            // Assigned via reassignment chain DA-0002 (Reassigned) -> DA-0003 (Assigned).
            O("TO-1010", "Bay of Plenty Kiwifruit Packers", "Mount Maunganui, Tauranga", "Penrose, Auckland",
                "Kiwifruit trays", 4_200, At(2, 7), At(2, 13), OrderPriority.Normal, OrderStatus.Assigned),
            // Assigned and in progress via DA-0004 - completion target.
            O("TO-1011", "Southern Cross Beverages", "East Tamaki, Auckland", "Pokeno",
                "Mixed beverages", 11_000, inProgressStart, inProgressEnd, OrderPriority.Normal, OrderStatus.Assigned),
            // Completed via DA-0005.
            O("TO-1012", "Harbourside Retail Group", "Wiri, Manukau", "Cambridge",
                "Seasonal stock", 6_500, At(-2, 7), At(-2, 12), OrderPriority.Normal, OrderStatus.Completed),
            // Cancelled order (by the customer) - never dispatched.
            O("TO-1013", "Cambridge Equine Feeds", "Cambridge", "Penrose, Auckland",
                "Bagged horse feed", 3_000, At(3, 8), At(3, 12), OrderPriority.Low, OrderStatus.Cancelled),
            // Pending again after DA-0006 was cancelled; DRV-010 / VEH-01 are free for this window.
            O("TO-1014", "Tamaki Pharmaceuticals", "East Tamaki, Auckland", "Wiri, Manukau",
                "Medical consumables", 800, At(2, 10), At(2, 14), OrderPriority.High, OrderStatus.Pending),
            // Back-to-back boundary: starts exactly when DA-0001 ends, so DRV-002 / VEH-05 are eligible.
            O("TO-1015", "Kauri Coast Foods", "Te Rapa, Hamilton", "Mount Maunganui, Tauranga",
                "Packaged dry goods (return leg)", 9_000, At(1, 14), At(1, 18), OrderPriority.Normal, OrderStatus.Pending),
            // Assigned via DA-0007 - cancel/reassign target.
            O("TO-1016", "Waikato Building Supplies", "Penrose, Auckland", "Cambridge",
                "Roofing iron", 4_800, At(3, 7), At(3, 12), OrderPriority.Normal, OrderStatus.Assigned),
            // Heavy load needing a Heavy Rigid >= 12,500 kg (VEH-06) or Heavy Combination (VEH-07).
            O("TO-1017", "Pokeno Dairy Co-operative", "Pokeno", "Penrose, Auckland",
                "Milk powder, 25 kg bags", 12_500, At(4, 6), At(4, 12), OrderPriority.Normal, OrderStatus.Pending),
        };
        TransportOrder Ord(string number) => orders.Single(o => o.OrderNumber == number);

        // ---------------- Assignments ----------------
        DispatchAssignment A(int n, string order, string driver, string vehicle, User createdBy, DateTimeOffset createdAt)
        {
            var o = Ord(order);
            return new DispatchAssignment
            {
                AssignmentNumber = $"DA-{n:D4}", Order = o, Driver = Drv(driver), Vehicle = Veh(vehicle),
                StartAt = o.PickupAt, EndAt = o.DeliveryDeadline, Status = AssignmentStatus.Assigned,
                CreatedAt = createdAt, CreatedBy = createdBy,
            };
        }
        void Close(DispatchAssignment a, AssignmentStatus status, User by, DateTimeOffset at, string? reason)
        {
            a.Status = status; a.ClosedBy = by; a.ClosedAt = at; a.CloseReason = reason;
        }

        var da1 = A(1, "TO-1009", "DRV-002", "VEH-05", dispatcher, nowUtc.AddDays(-2));

        var da2 = A(2, "TO-1010", "DRV-004", "VEH-03", dispatcher, nowUtc.AddDays(-2));
        Close(da2, AssignmentStatus.Reassigned, dispatcher, nowUtc.AddDays(-1), "Driver requested a schedule change.");
        var da3 = A(3, "TO-1010", "DRV-008", "VEH-10", dispatcher, nowUtc.AddDays(-1));
        da3.PreviousAssignment = da2;

        var da4 = A(4, "TO-1011", "DRV-009", "VEH-09", dispatcher, nowUtc.AddDays(-1));
        da4.Notes = "Driver to call the Pokeno depot 30 minutes before arrival.";

        var da5 = A(5, "TO-1012", "DRV-003", "VEH-06", dispatcher, nowUtc.AddDays(-3));
        Close(da5, AssignmentStatus.Completed, dispatcher, Ord("TO-1012").DeliveryDeadline.AddMinutes(-30), "Delivered on time.");

        var da6 = A(6, "TO-1014", "DRV-010", "VEH-01", admin, nowUtc.AddDays(-2));
        Close(da6, AssignmentStatus.Cancelled, admin, nowUtc.AddDays(-1), "Booked against the wrong customer account.");

        var da7 = A(7, "TO-1016", "DRV-001", "VEH-03", dispatcher2, nowUtc.AddDays(-1));

        var assignments = new List<DispatchAssignment> { da1, da2, da3, da4, da5, da6, da7 };

        return new Dataset(users, drivers, vehicles, orders, assignments);
    }
}
