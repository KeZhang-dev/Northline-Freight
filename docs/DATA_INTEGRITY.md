# Data Integrity, Transactions and Concurrency

The dispatch rules are protected at two levels:

1. **Service level** (`DispatchService`). Every rule is checked inside a transaction, with row locks, so the API can return a precise, readable error.
2. **Database level** (PostgreSQL constraints). The database has the **final say**. Even if application code were bypassed or two requests raced past the service checks, duplicate active assignments and overlapping driver or vehicle allocations cannot be stored.

---

## 1. Transactions

Every write operation (create, reassign, cancel, complete) runs in **one database transaction** at PostgreSQL's default Read Committed isolation. If anything fails before commit, whether a business rule, a constraint or an unexpected error, the whole transaction is rolled back. No partial changes are left behind (BR-09).

| Operation | Steps inside the transaction |
|---|---|
| **Create** | lock order → BR-10 check → lock driver → lock vehicle → BR-01 to BR-07 → insert assignment → set order to `Assigned` → commit |
| **Reassign** | lock order → lock assignment → check version → check status → lock new driver → lock new vehicle → BR-11 rules → **save the old record as `Reassigned` first** → insert the new `Assigned` record → commit |
| **Cancel** | lock order → lock assignment → check version → check status → close as `Cancelled` → order to `Pending` → commit |
| **Complete** | lock order → lock assignment → check version → check status → close as `Completed` → order to `Completed` → commit |

On reassign, the old record is saved before the new one is inserted. PostgreSQL checks constraints statement by statement, so the old row must stop being `Assigned` before the replacement, which uses the same order and possibly the same driver or vehicle, is inserted.

## 2. Row locking

Rows are locked with `SELECT … FOR UPDATE` (order and assignment rows) or `SELECT … FOR NO KEY UPDATE` (driver and vehicle rows). Locks are always taken in the same order to avoid deadlocks:

```
order  →  assignment  →  driver  →  vehicle
```

| Lock | Protects against |
|---|---|
| Order row | Two requests assigning the same order at once. The second waits, then sees the committed assignment and fails with `409 ORDER_ALREADY_ASSIGNED`. It also makes create, cancel and reassign on the same order run one after another. |
| Assignment row | Two dispatchers changing the same assignment at once (for example cancel and reassign). The second waits, then fails with `409 ASSIGNMENT_MODIFIED` or `409 INVALID_STATUS_TRANSITION`. |
| Driver / vehicle row | Two requests allocating the same driver or vehicle to overlapping jobs on **different** orders. The second waits, then its conflict check sees the committed assignment and fails with a precise `422 DRIVER_SCHEDULE_CONFLICT` / `VEHICLE_SCHEDULE_CONFLICT`. Without this lock, two concurrent inserts could deadlock inside the exclusion constraint. |

`FOR NO KEY UPDATE` is used on driver and vehicle rows because it does not block the foreign-key checks made when assignment rows are inserted.

In V1, driver and vehicle status cannot be changed through the API, so these locks only serialise allocation.

## 3. Detecting conflicting edits to the same assignment

- `dispatch_assignments` and `transport_orders` use PostgreSQL's built-in `xmin` system column as an EF Core concurrency token (`uint Version`).
- Every assignment returned by the API includes its `version`.
- Reassign, cancel and complete **require** the `version` the client last saw (a missing version returns 400).
- After the row lock is taken, the stored version is compared with the request. If they differ, the response is `409 ASSIGNMENT_MODIFIED` and nothing changes.
- As a backstop, EF Core includes `xmin` in the `WHERE` clause of every update, so a lost update would raise `DbUpdateConcurrencyException`, which is mapped to `409 ASSIGNMENT_MODIFIED`.

Because the version is checked before the status, a client acting on a stale view always gets `ASSIGNMENT_MODIFIED`, even if the assignment has since been closed.

## 4. Database constraints

All of these are created by the EF Core migration `backend/src/Northline.Api/Data/Migrations/*_InitialCreate.cs`.

| Constraint | Definition | Purpose | How it is created |
|---|---|---|---|
| `btree_gist` extension | `CREATE EXTENSION btree_gist` | Lets an integer column use `=` inside a GiST exclusion constraint | `modelBuilder.HasPostgresExtension("btree_gist")` in `NorthlineDbContext` |
| `ux_assignment_active_order` | `UNIQUE (order_id) WHERE status = 'Assigned'` | BR-10: at most one active assignment per order | `HasIndex(a => a.OrderId).IsUnique().HasFilter("status = 'Assigned'")` |
| `ux_assignment_previous` | `UNIQUE (previous_assignment_id) WHERE previous_assignment_id IS NOT NULL` | An assignment can be replaced only once, so history is a simple chain | `HasIndex(...).IsUnique().HasFilter(...)` |
| `ex_assignment_driver_overlap` | `EXCLUDE USING gist (driver_id WITH =, tstzrange(start_at, end_at, '[)') WITH &&) WHERE (status = 'Assigned')` | BR-05: no overlapping active assignments for a driver | **Raw SQL** `migrationBuilder.Sql(DbConstraints.CreateDriverOverlapSql)` |
| `ex_assignment_vehicle_overlap` | same, using `vehicle_id` | BR-06: no overlapping active assignments for a vehicle | **Raw SQL** `migrationBuilder.Sql(DbConstraints.CreateVehicleOverlapSql)` |
| `ck_assignment_window` | `CHECK (end_at > start_at)` | Assignments always have a valid window | `HasCheckConstraint` |
| `ck_assignment_closed_fields` | `Assigned` ⇒ no close fields; `Completed` ⇒ `closed_at` and `closed_by` set; `Cancelled`/`Reassigned` ⇒ `closed_at`, `closed_by` **and** `close_reason` set | Audit data is always complete | `HasCheckConstraint` |
| `ck_driver_licence_class` | `licence_class BETWEEN 1 AND 5` | Valid licence classes | `HasCheckConstraint` |
| `ck_vehicle_payload_positive` | `max_payload_kg > 0` | Valid payloads | `HasCheckConstraint` |

The range `'[)'` includes the start and excludes the end, matching the application's overlap definition, so back-to-back jobs are allowed. Only `Assigned` rows take part, so cancelled, reassigned and completed history never blocks future allocations.

There is deliberately **no** check constraint on `transport_orders` requiring `pickup_at < delivery_deadline`. An order with an invalid window (TO-1008) is a seeded negative scenario that BR-01 must reject at assignment time.

### How the exclusion constraints get into the migration

EF Core has no fluent API for `EXCLUDE` constraints. The DDL is kept in `Data/DbConstraints.cs` and applied by hand-written lines at the end of the generated migration:

```csharp
// Up()
migrationBuilder.Sql(DbConstraints.CreateDriverOverlapSql);
migrationBuilder.Sql(DbConstraints.CreateVehicleOverlapSql);

// Down()
migrationBuilder.Sql(DbConstraints.DropVehicleOverlapSql);
migrationBuilder.Sql(DbConstraints.DropDriverOverlapSql);
```

If a future migration rebuilds `dispatch_assignments`, these lines must be carried over. The integration tests (`Database_rejects_overlapping_driver_assignment_written_directly`) fail if the constraint is missing.

### Inspecting the constraints

```bash
docker exec -it northline-db psql -U northline -d northline -c "\d dispatch_assignments"
```

## 5. Mapping database errors to API errors

`Common/PostgresErrorTranslator.cs` looks through the whole exception chain (EF Core and Npgsql may wrap errors several levels deep):

| PostgreSQL error | Constraint | API response |
|---|---|---|
| `23505` unique violation | `ux_assignment_active_order` | 409 `ORDER_ALREADY_ASSIGNED` (BR-10) |
| `23505` unique violation | `ux_assignment_previous` | 409 `ASSIGNMENT_MODIFIED` |
| `23P01` exclusion violation | `ex_assignment_driver_overlap` | 409 `DRIVER_SCHEDULE_CONFLICT` (BR-05) |
| `23P01` exclusion violation | `ex_assignment_vehicle_overlap` | 409 `VEHICLE_SCHEDULE_CONFLICT` (BR-06) |
| `40P01` deadlock / `40001` serialization failure | — | 409 `CONCURRENCY_CONFLICT` (retry) |
| EF `DbUpdateConcurrencyException` | — | 409 `ASSIGNMENT_MODIFIED` |

With the locking described above, these database-level errors should not happen through the normal API path. They are the safety net.

## 6. Assignment numbers

`DA-####` numbers come from the PostgreSQL sequence `assignment_number_seq`. After seeding, the sequence is set so the next number is `DA-0008`. Sequences are not rolled back with transactions, so a request that fails only at the database-constraint stage (a race) uses up a number. Gaps in assignment numbers are therefore possible and expected. Requests rejected by the normal business-rule checks do **not** use up a number.

## 7. Developer tests

`backend/tests/Northline.Api.Tests` (xUnit, separate from any QA test project) includes integration tests that run against a real PostgreSQL started with Testcontainers (Docker required). They cover:
- the overlap and duplicate constraints, by writing directly to the database and bypassing the service;
- full rollback when a rule fails;
- concurrent create requests for the same driver, and for the same order;
- concurrent cancel and reassign of the same assignment.
