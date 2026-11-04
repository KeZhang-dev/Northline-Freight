# Business Rules — Dispatch Management

This document defines the behaviour of the Dispatch Management module, the only fully functional module in Northline TMS V1. Each rule is enforced on the **backend**. The frontend shows the same information as guidance only.

Implementation references:
- Pure rule evaluation: `backend/src/Northline.Api/Validation/DispatchRuleEvaluator.cs`
- Transactions, locking and lifecycle: `backend/src/Northline.Api/Services/DispatchService.cs`
- Database constraints: see [DATA_INTEGRITY.md](DATA_INTEGRITY.md)

---

## 1. Definitions

| Term | Definition |
|---|---|
| **Assignment window** | The time span from `Order.PickupAt` (included) up to `Order.DeliveryDeadline` (excluded): `[PickupAt, DeliveryDeadline)`. It is copied to the assignment's `StartAt` and `EndAt`. |
| **Overlap** | Windows A and B overlap when `A.Start < B.End` **and** `B.Start < A.End`. Back-to-back windows, where one ends exactly when the next starts, do **not** overlap. |
| **Active assignment** | An assignment with status `Assigned`. Only active assignments occupy driver or vehicle time. `Reassigned`, `Cancelled` and `Completed` assignments never block anything. |
| **Operational driver** | `EmploymentStatus = Active` (not `OnLeave`, not `Inactive`). |
| **Operational vehicle** | `VehicleStatus = Active` (not `Maintenance`, not `Inactive`). |
| **Available now** | Operational **and** with no active assignment whose window contains the current time. This is shown in the UI and the KPIs; it is not used for validation. |
| **Now** | The server's current time (UTC). All times are stored in UTC and shown in New Zealand time (`Pacific/Auckland`). |

There is **no stored availability flag** on drivers or vehicles. Whether a driver or vehicle can take a job is always worked out from its operational status and its active assignments.

---

## 2. Rules

### BR-01 Order eligibility
An order can receive a new assignment only if **all** of these are true:
1. `Status = Pending`. Orders that are `OnHold`, `Assigned`, `Completed` or `Cancelled` cannot be assigned.
2. `DeliveryDeadline > now`. Expired orders cannot be assigned.
3. `PickupAt < DeliveryDeadline`. An order with an invalid time window cannot be assigned.

| Condition | Error code |
|---|---|
| Status is not Pending | `ORDER_NOT_PENDING` |
| Deadline has passed (or is exactly now) | `ORDER_EXPIRED` |
| Pickup is not earlier than the deadline | `ORDER_INVALID_TIME_WINDOW` |

An order whose pickup time has passed but whose deadline has not is still eligible.

### BR-02 Driver availability
The driver must be operational (`EmploymentStatus = Active`). Otherwise: `DRIVER_NOT_ACTIVE`.

### BR-03 Vehicle availability
The vehicle must be operational (`VehicleStatus = Active`). Otherwise: `VEHICLE_NOT_ACTIVE`.

### BR-04 Vehicle capacity
`Vehicle.MaxPayloadKg >= Order.CargoWeightKg`. Equal values pass. Otherwise: `VEHICLE_CAPACITY_EXCEEDED`.

### BR-05 Driver scheduling conflict
The driver must have no other active assignment that overlaps the order's window. Otherwise: `DRIVER_SCHEDULE_CONFLICT`, with one violation per conflicting assignment.

### BR-06 Vehicle scheduling conflict
The vehicle must have no other active assignment that overlaps the order's window. Otherwise: `VEHICLE_SCHEDULE_CONFLICT`, with one violation per conflicting assignment.

If the order's time window is invalid (BR-01.3), the schedule checks are skipped, because there is no meaningful window to compare.

### BR-07 Driver qualification (simplified licence rules)
The driver's licence class must be at least the minimum class for the vehicle type. A higher class covers all lower classes. Otherwise: `DRIVER_LICENCE_INCOMPATIBLE`.

| Vehicle type | Minimum licence class |
|---|---|
| Light Commercial Vehicle (`LightCommercial`) | 1 |
| Medium Rigid Truck (`MediumRigid`) | 2 |
| Heavy Rigid Truck (`HeavyRigid`) | 4 |
| Heavy Combination Vehicle (`HeavyCombination`) | 5 |

> **Important — simplified, project-specific rules.** These rules exist only for this training application. They are **not** a complete or accurate representation of New Zealand driver licensing law. Learner, restricted and full licence stages, endorsements, gross laden mass thresholds, work-time rules and other compliance requirements are deliberately ignored.

The table can be configured in `appsettings.json` under `Dispatch:MinimumLicenceClass`. The API refuses to start if a vehicle type is missing or a value is outside 1–5.

### BR-08 Successful assignment
When every rule passes, a single transaction:
- creates a new assignment with status `Assigned`, a new `AssignmentNumber` (`DA-####`), the order's window, `CreatedAt` and `CreatedBy`;
- changes the order from `Pending` to `Assigned`.

### BR-09 Failed assignment
If any rule fails, nothing is written:
- no assignment is created;
- the order is unchanged;
- driver and vehicle schedules are unchanged.

The response lists **every** violated rule from BR-01 to BR-07, not just the first one (see §4).

### BR-10 Duplicate assignment prevention
An order can have at most one active assignment. A create request for an order that already has one is rejected with `409 ORDER_ALREADY_ASSIGNED`. To change the driver or vehicle, reassign instead. The database also enforces this rule (a partial unique index).

### BR-11 Reassignment
An active assignment can be moved to a different driver, a different vehicle, or both.

Requirements:
- The assignment's status is `Assigned`, and the request's `version` matches its current version.
- A `reason` is given (required, up to 500 characters).
- At least one of the driver or vehicle changes. Otherwise: `422 REASSIGN_NO_CHANGE`.
- The order's deadline has not passed (`ORDER_EXPIRED`), and its window is valid.
- The new driver and vehicle pass BR-02 to BR-07. The assignment being replaced is **left out** of the conflict checks, so keeping the same driver while changing the vehicle is valid.

Effect, in one transaction:
1. The existing record is **kept**: its status becomes `Reassigned`, and `ClosedAt`, `ClosedBy` and `CloseReason` are filled in.
2. A **new** `Assigned` record is created with `PreviousAssignmentId` pointing to the old one, its own `CreatedAt` and `CreatedBy`, and the notes copied over.
3. The order stays `Assigned`.

An assignment can be replaced only once, so the history is a simple chain: `DA-0002 → DA-0003 → …`. `GET /api/assignments/{id}` returns every assignment for the order, oldest first.

### BR-12 Cancellation
An active assignment can be cancelled. A `reason` is required.

Effect, in one transaction:
- The assignment's status becomes `Cancelled`, and `ClosedAt`, `ClosedBy` and `CloseReason` are filled in. The record is kept.
- The order goes back to `Pending` and can be dispatched again.
- The driver and vehicle are released for that window straight away, because cancelled records do not count in conflict checks or database constraints.

### Completion
An active assignment can be marked completed. The `reason` (completion note) is optional. There is no time restriction in V1: completion is allowed whenever the status is `Assigned`.

Effect: the assignment becomes `Completed` with `ClosedAt` and `ClosedBy` filled in (and `CloseReason` if a note was given), and the order becomes `Completed`.

---

## 3. Assignment lifecycle

```
                 ┌── cancel   (reason required) ──▶ Cancelled   ── order → Pending
                 │
  (create) ──▶ Assigned ── reassign (reason required) ─▶ Reassigned ── + new Assigned record (PreviousAssignmentId → old)
                 │                                                      order stays Assigned
                 └── complete (note optional) ──▶ Completed   ── order → Completed
```

| From \ Action | cancel | reassign | complete |
|---|---|---|---|
| **Assigned** | → Cancelled | → Reassigned (+ new Assigned) | → Completed |
| **Reassigned** | 409 `INVALID_STATUS_TRANSITION` | 409 | 409 |
| **Cancelled** | 409 | 409 | 409 |
| **Completed** | 409 | 409 | 409 |

`Reassigned`, `Cancelled` and `Completed` are final. Assignment records are never deleted, and the API has no DELETE endpoint for them.

### Order status changes caused by dispatch

| Event | Order status |
|---|---|
| Assignment created | Pending → Assigned |
| Assignment reassigned | stays Assigned |
| Assignment cancelled | Assigned → Pending |
| Assignment completed | Assigned → Completed |

`OnHold` and `Cancelled` order statuses exist only in the seed data. Changing an order's status directly belongs to the Transport Orders module, which is out of scope for V1.

---

## 4. Checking order and response codes

**Create** (`POST /api/assignments`):
1. `400 VALIDATION_FAILED`: malformed or missing fields.
2. `404 ORDER_NOT_FOUND` / `DRIVER_NOT_FOUND` / `VEHICLE_NOT_FOUND`.
3. `409 ORDER_ALREADY_ASSIGNED` (BR-10).
4. `422 ASSIGNMENT_VALIDATION_FAILED`: every BR-01 to BR-07 failure, listed in `violations[]`.
5. `409` `DRIVER_SCHEDULE_CONFLICT`, `VEHICLE_SCHEDULE_CONFLICT` or `ORDER_ALREADY_ASSIGNED`: only when a database constraint catches a race that got past the checks above (see DATA_INTEGRITY.md).
6. `201 Created`.

**Reassign / cancel / complete**:
1. `400 VALIDATION_FAILED`: for example, a missing `version` or a missing required `reason`.
2. `404 ASSIGNMENT_NOT_FOUND` (and, for reassign, `DRIVER_NOT_FOUND` / `VEHICLE_NOT_FOUND`).
3. `409 ASSIGNMENT_MODIFIED`: the `version` sent is not the current version. This is checked **before** the status.
4. `409 INVALID_STATUS_TRANSITION`: the assignment is not `Assigned`.
5. Reassign only: `422 ASSIGNMENT_VALIDATION_FAILED` (`REASSIGN_NO_CHANGE`, BR-01 timing, BR-02 to BR-07).
6. `200 OK` (cancel, complete) or `201 Created` (reassign).

Each violation in `violations[]` contains `rule` (for example `BR-04`), `code` and a readable `message` that names the order, driver, vehicle or conflicting assignment involved, with times in NZ local time.

---

## 5. Roles

| Capability | Admin | Dispatcher | Driver |
|---|:-:|:-:|:-:|
| Sign in | ✓ | ✓ | ✓ |
| Dashboard and placeholder modules | ✓ | ✓ | ✗ (403) |
| View orders, drivers, vehicles, assignments | ✓ | ✓ | ✗ (403) |
| Create, reassign, cancel, complete assignments | ✓ | ✓ | ✗ (403) |
| Driver landing page (`/driver`, `GET /api/driver/me`) | ✗ | ✗ | ✓ |
| `POST /api/dev/reset` (Development only) | ✓ | ✗ | ✗ |

Unauthenticated requests to protected endpoints return `401 UNAUTHORIZED`.

---

## 6. Advisory candidate view

`GET /api/orders/{id}/candidates` evaluates BR-01 to BR-07 for every driver and vehicle and returns `eligible` and `reasons[]` for each. The UI shows ineligible resources with their reasons but does not let you select them. This view is **advisory only**: every create and reassign request is validated again on the server, inside the transaction, regardless of what the UI showed.
