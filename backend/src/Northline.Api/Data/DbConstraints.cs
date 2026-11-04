namespace Northline.Api.Data;

/// <summary>
/// Names and DDL of the database constraints that enforce dispatch integrity.
/// PostgresErrorTranslator maps violations of these to API error codes.
/// </summary>
public static class DbConstraints
{
    public const string ActiveOrderUnique = "ux_assignment_active_order";
    public const string PreviousAssignmentUnique = "ux_assignment_previous";
    public const string DriverOverlap = "ex_assignment_driver_overlap";
    public const string VehicleOverlap = "ex_assignment_vehicle_overlap";
    public const string AssignmentWindow = "ck_assignment_window";
    public const string AssignmentClosedFields = "ck_assignment_closed_fields";
    public const string AssignmentNumberSequence = "assignment_number_seq";

    // EF Core has no fluent API for EXCLUDE constraints, so these are applied with
    // migrationBuilder.Sql(...) in the InitialCreate migration. The range is half-open [start, end),
    // so back-to-back assignments are allowed. Only active ('Assigned') rows take part.

    public const string CreateDriverOverlapSql =
        "ALTER TABLE dispatch_assignments ADD CONSTRAINT " + DriverOverlap +
        " EXCLUDE USING gist (driver_id WITH =, tstzrange(start_at, end_at, '[)') WITH &&)" +
        " WHERE (status = 'Assigned');";

    public const string CreateVehicleOverlapSql =
        "ALTER TABLE dispatch_assignments ADD CONSTRAINT " + VehicleOverlap +
        " EXCLUDE USING gist (vehicle_id WITH =, tstzrange(start_at, end_at, '[)') WITH &&)" +
        " WHERE (status = 'Assigned');";

    public const string DropDriverOverlapSql =
        "ALTER TABLE dispatch_assignments DROP CONSTRAINT IF EXISTS " + DriverOverlap + ";";

    public const string DropVehicleOverlapSql =
        "ALTER TABLE dispatch_assignments DROP CONSTRAINT IF EXISTS " + VehicleOverlap + ";";
}
