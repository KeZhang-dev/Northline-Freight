using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Northline.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateSequence(
                name: "assignment_number_seq");

            migrationBuilder.CreateTable(
                name: "drivers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    driver_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    licence_class = table.Column<int>(type: "integer", nullable: false),
                    employment_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_drivers", x => x.id);
                    table.CheckConstraint("ck_driver_licence_class", "licence_class BETWEEN 1 AND 5");
                });

            migrationBuilder.CreateTable(
                name: "transport_orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    order_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    pickup_location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    delivery_location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cargo_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    cargo_weight_kg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    pickup_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    delivery_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    priority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transport_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vehicle_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    registration_number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    vehicle_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    max_payload_kg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    vehicle_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehicles", x => x.id);
                    table.CheckConstraint("ck_vehicle_payload_positive", "max_payload_kg > 0");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    driver_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_drivers_driver_id",
                        column: x => x.driver_id,
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_assignments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    assignment_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    order_id = table.Column<int>(type: "integer", nullable: false),
                    driver_id = table.Column<int>(type: "integer", nullable: false),
                    vehicle_id = table.Column<int>(type: "integer", nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    previous_assignment_id = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    close_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dispatch_assignments", x => x.id);
                    table.CheckConstraint("ck_assignment_closed_fields", "(status = 'Assigned' AND closed_at IS NULL AND closed_by_user_id IS NULL AND close_reason IS NULL) OR (status = 'Completed' AND closed_at IS NOT NULL AND closed_by_user_id IS NOT NULL) OR (status IN ('Cancelled', 'Reassigned') AND closed_at IS NOT NULL AND closed_by_user_id IS NOT NULL AND close_reason IS NOT NULL)");
                    table.CheckConstraint("ck_assignment_window", "end_at > start_at");
                    table.ForeignKey(
                        name: "fk_dispatch_assignments_dispatch_assignments_previous_assignme",
                        column: x => x.previous_assignment_id,
                        principalTable: "dispatch_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dispatch_assignments_drivers_driver_id",
                        column: x => x.driver_id,
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dispatch_assignments_transport_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "transport_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dispatch_assignments_users_closed_by_user_id",
                        column: x => x.closed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dispatch_assignments_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dispatch_assignments_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_assignments_assignment_number",
                table: "dispatch_assignments",
                column: "assignment_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_assignments_closed_by_user_id",
                table: "dispatch_assignments",
                column: "closed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_assignments_created_by_user_id",
                table: "dispatch_assignments",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_assignments_driver_id",
                table: "dispatch_assignments",
                column: "driver_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_assignments_status",
                table: "dispatch_assignments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_assignments_vehicle_id",
                table: "dispatch_assignments",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "ux_assignment_active_order",
                table: "dispatch_assignments",
                column: "order_id",
                unique: true,
                filter: "status = 'Assigned'");

            migrationBuilder.CreateIndex(
                name: "ux_assignment_previous",
                table: "dispatch_assignments",
                column: "previous_assignment_id",
                unique: true,
                filter: "previous_assignment_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_drivers_driver_code",
                table: "drivers",
                column: "driver_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transport_orders_order_number",
                table: "transport_orders",
                column: "order_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transport_orders_status",
                table: "transport_orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_users_driver_id",
                table: "users",
                column: "driver_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_registration_number",
                table: "vehicles",
                column: "registration_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_vehicle_code",
                table: "vehicles",
                column: "vehicle_code",
                unique: true);

            // Hand-written: EF Core has no fluent API for EXCLUDE constraints.
            // BR-05 / BR-06 - no two active assignments may overlap for the same driver or vehicle.
            // See DbConstraints and docs/DATA_INTEGRITY.md.
            migrationBuilder.Sql(DbConstraints.CreateDriverOverlapSql);
            migrationBuilder.Sql(DbConstraints.CreateVehicleOverlapSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DbConstraints.DropVehicleOverlapSql);
            migrationBuilder.Sql(DbConstraints.DropDriverOverlapSql);

            migrationBuilder.DropTable(
                name: "dispatch_assignments");

            migrationBuilder.DropTable(
                name: "transport_orders");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "vehicles");

            migrationBuilder.DropTable(
                name: "drivers");

            migrationBuilder.DropSequence(
                name: "assignment_number_seq");
        }
    }
}
