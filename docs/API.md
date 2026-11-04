# API Reference

Base URL:
- Local source: `http://localhost:5080/api`
- Docker stack: `http://localhost:5080/api` (direct) or `http://localhost:8080/api` (through the web container)

Interactive documentation (Swagger UI): `http://localhost:5080/swagger`. Call `POST /api/auth/login`, copy the `token`, click **Authorize** and paste it in.

## Conventions

| Topic | Convention |
|---|---|
| Format | JSON, `Content-Type: application/json` |
| Authentication | `Authorization: Bearer <JWT>`; tokens last 8 hours (`Jwt:ExpiryMinutes`) |
| Enums | Strings, for example `"Pending"`, `"HeavyRigid"`. Case-insensitive in query strings |
| Times | ISO 8601 in UTC, for example `2026-11-05T01:00:00+00:00` |
| Weights | Kilograms, as decimal numbers |
| Paging | `page` (from 1) and `pageSize` (1–100, default 25). The response has `items`, `page`, `pageSize`, `totalCount`, `totalPages` |
| Sorting | `sort=<field>&dir=Asc\|Desc` |
| Search | `search=` is a case-insensitive "contains" match across the fields listed per endpoint |
| Unknown fields / routes | Unknown JSON fields are ignored; unknown routes return a 404 problem response |

## Error format

Every error is an RFC 7807 problem response (`application/problem+json`):

```json
{
  "type": "https://httpstatuses.io/422",
  "title": "Assignment validation failed",
  "status": 422,
  "detail": "2 business rules were violated. See 'violations' for details.",
  "instance": "/api/assignments",
  "code": "ASSIGNMENT_VALIDATION_FAILED",
  "violations": [
    { "rule": "BR-05", "code": "DRIVER_SCHEDULE_CONFLICT", "message": "Driver DRV-002 (James Wilson) is already allocated to DA-0001 (Thu 5 Nov 2026 06:00 – Thu 5 Nov 2026 14:00), which overlaps this job." },
    { "rule": "BR-06", "code": "VEHICLE_SCHEDULE_CONFLICT", "message": "Vehicle VEH-05 (JRT604) is already allocated to DA-0001 (...), which overlaps this job." }
  ],
  "traceId": "0HNPPUOM3D3C5:00000001"
}
```

`detail` is a readable summary. If there is exactly one violation, `detail` is that violation's message. `violations` is always present, possibly empty.

### Status codes

| Status | Meaning |
|---|---|
| 200 / 201 | Success (201 for create and reassign, with a `Location` header) |
| 400 | Request validation failed (`VALIDATION_FAILED`). Each field error appears in `violations` with `rule: "REQUEST"` |
| 401 | Missing, invalid or expired token (`UNAUTHORIZED`), or wrong login details (`INVALID_CREDENTIALS`) |
| 403 | Signed in, but the role is not allowed (`FORBIDDEN`) |
| 404 | Resource not found |
| 409 | State conflict: duplicate, invalid transition, stale version, concurrent update |
| 422 | Business rule violation(s) |
| 500 | Unexpected error (`INTERNAL_ERROR`). Quote the `traceId` when reporting it |

### Error codes

| Code | Status | Rule | Meaning |
|---|---|---|---|
| `VALIDATION_FAILED` | 400 | — | Missing or malformed request fields |
| `INVALID_CREDENTIALS` | 401 | — | Email or password incorrect (same response for unknown email) |
| `UNAUTHORIZED` | 401 | — | No valid bearer token |
| `FORBIDDEN` | 403 | — | Role not permitted |
| `NOT_FOUND` | 404 | — | Route or resource not found |
| `ORDER_NOT_FOUND`, `DRIVER_NOT_FOUND`, `VEHICLE_NOT_FOUND`, `ASSIGNMENT_NOT_FOUND` | 404 | — | ID does not exist |
| `ASSIGNMENT_VALIDATION_FAILED` | 422 | — | Overall code for business-rule failures; see `violations` |
| `ORDER_NOT_PENDING` | 422 (violation) | BR-01 | Order status is not Pending |
| `ORDER_EXPIRED` | 422 (violation) | BR-01 | Delivery deadline has passed |
| `ORDER_INVALID_TIME_WINDOW` | 422 (violation) | BR-01 | Pickup is not earlier than the deadline |
| `DRIVER_NOT_ACTIVE` | 422 (violation) | BR-02 | Driver is OnLeave or Inactive |
| `VEHICLE_NOT_ACTIVE` | 422 (violation) | BR-03 | Vehicle is in Maintenance or Inactive |
| `VEHICLE_CAPACITY_EXCEEDED` | 422 (violation) | BR-04 | Payload below cargo weight |
| `DRIVER_SCHEDULE_CONFLICT` | 422 (violation) / 409 | BR-05 | Overlapping active assignment for the driver (409 when caught by the database constraint) |
| `VEHICLE_SCHEDULE_CONFLICT` | 422 (violation) / 409 | BR-06 | Overlapping active assignment for the vehicle (409 when caught by the database constraint) |
| `DRIVER_LICENCE_INCOMPATIBLE` | 422 (violation) | BR-07 | Licence class too low for the vehicle type |
| `ORDER_ALREADY_ASSIGNED` | 409 | BR-10 | Order already has an active assignment |
| `REASSIGN_NO_CHANGE` | 422 (violation) | BR-11 | Reassignment changes neither driver nor vehicle |
| `INVALID_STATUS_TRANSITION` | 409 | LIFECYCLE | Assignment is not `Assigned` |
| `ASSIGNMENT_MODIFIED` | 409 | LIFECYCLE | The `version` sent is stale |
| `CONCURRENCY_CONFLICT` | 409 | — | Deadlock or serialization failure; retry |
| `INTERNAL_ERROR` | 500 | — | Unexpected error |

---

## Endpoints

Role key: **A** = Admin, **D** = Dispatcher, **Dr** = Driver.

| Method | Path | Roles | Summary |
|---|---|---|---|
| POST | `/auth/login` | public | Sign in |
| GET | `/auth/me` | any signed-in user | Current user |
| GET | `/health` | public | Health check |
| GET | `/dispatch/summary` | A, D | Dispatch KPIs |
| GET | `/dashboard/stats` | A, D | Sample dashboard statistics |
| GET | `/orders` | A, D | List orders |
| GET | `/orders/{id}` | A, D | Order and its assignment history |
| GET | `/orders/{id}/candidates` | A, D | Driver and vehicle eligibility for an order |
| GET | `/drivers` | A, D | List drivers |
| GET | `/drivers/{id}` | A, D | Driver and their assignments |
| GET | `/vehicles` | A, D | List vehicles |
| GET | `/vehicles/{id}` | A, D | Vehicle and its assignments |
| GET | `/assignments` | A, D | List assignments |
| GET | `/assignments/{id}` | A, D | Assignment detail and order history |
| POST | `/assignments` | A, D | Create assignment |
| POST | `/assignments/{id}/reassign` | A, D | Reassign |
| POST | `/assignments/{id}/cancel` | A, D | Cancel |
| POST | `/assignments/{id}/complete` | A, D | Complete |
| GET | `/driver/me` | Dr | Driver landing-page profile |
| POST | `/dev/reset` | A (Development only) | Reset and re-seed the database |

### POST /auth/login

```json
// request
{ "email": "dispatcher@northline.test", "password": "Northline!Dev2026" }
// 200
{ "token": "eyJ…", "expiresAt": "2026-11-04T06:00:00+00:00",
  "user": { "id": 2, "email": "dispatcher@northline.test", "fullName": "Rachel Thompson", "role": "Dispatcher", "driverId": null } }
```
The email match ignores case. A wrong password, an unknown email or a disabled account all return `401 INVALID_CREDENTIALS`.

### GET /dispatch/summary

```json
{ "asOf": "…", "pendingOrders": 10, "eligiblePendingOrders": 8, "urgentPendingOrders": 1, "assignedJobs": 4,
  "availableDrivers": 7, "activeDrivers": 8, "totalDrivers": 10,
  "availableVehicles": 7, "activeVehicles": 8, "totalVehicles": 10 }
```
- `urgentPendingOrders` counts Pending, Urgent orders whose deadline has not passed.
- `available*` counts drivers or vehicles that are Active and have no active assignment covering the current time.

### GET /orders

| Query | Type | Notes |
|---|---|---|
| `status` | OrderStatus | `Pending`, `OnHold`, `Assigned`, `Completed`, `Cancelled` |
| `priority` | OrderPriority | `Low`, `Normal`, `High`, `Urgent` |
| `eligibleOnly` | bool | Only orders that pass BR-01 now |
| `search` | string | Order number, customer, pickup, delivery, cargo description |
| `sort` | `PickupAt` (default), `DeliveryDeadline`, `Priority`, `CargoWeightKg`, `OrderNumber`, `CustomerName` | Priority sorts by importance: Low < Normal < High < Urgent |
| `dir`, `page`, `pageSize` | | |

Each item includes `isEligible`, `ineligibilityReasons[]` (BR-01 violations) and `activeAssignment` (an assignment reference or `null`).

### GET /orders/{id}/candidates

| Query | Notes |
|---|---|
| `vehicleId` | Also check each **driver's** licence against this vehicle (BR-07) |
| `driverId` | Also check each **vehicle's** licence requirement against this driver (BR-07) |
| `excludeAssignmentId` | Reassignment mode. Must be the order's active assignment (otherwise 409 `INVALID_STATUS_TRANSITION`); it is left out of the conflict checks and the order is checked with the reassignment rules |

Response: `order`, `mode` (`assign`/`reassign`), `orderEligible`, `orderViolations[]` (in assign mode this includes BR-10 if the order is already assigned), `drivers[]` and `vehicles[]`, each with `eligible` and `reasons[]`, eligible ones first. **Advisory only**: create and reassign validate independently.

### GET /drivers, GET /vehicles

`/drivers` query: `employmentStatus`, `availableNow`, `search` (code, name, email, phone).
`/vehicles` query: `vehicleStatus`, `vehicleType`, `availableNow`, `search` (code, registration).

Neither list is paged. Each item includes the worked-out `isAvailableNow`, `currentAssignment`, `nextAssignment` and `upcomingAssignmentCount`. Drivers also include `permittedVehicleTypes`; vehicles include `minimumLicenceClass`.

### GET /assignments

| Query | Notes |
|---|---|
| `status` | `Assigned`, `Reassigned`, `Cancelled`, `Completed` |
| `priority` | The order's priority |
| `driverId`, `vehicleId`, `orderId` | Filters |
| `search` | Assignment number, order number, customer, driver code or name, vehicle code or registration |
| `sort` | `StartAt` (default), `CreatedAt`, `AssignmentNumber`, `Status`, `Priority`, `OrderNumber`, `DriverName` |
| `dir`, `page`, `pageSize` | |

### Assignment object

```json
{
  "id": 3, "assignmentNumber": "DA-0003", "status": "Assigned", "version": 812,
  "startAt": "…", "endAt": "…",
  "order":   { "id": 10, "orderNumber": "TO-1010", "customerName": "…", "pickupLocation": "…", "deliveryLocation": "…",
               "cargoWeightKg": 4200.00, "pickupAt": "…", "deliveryDeadline": "…", "priority": "Normal", "status": "Assigned" },
  "driver":  { "id": 8, "driverCode": "DRV-008", "fullName": "Emma Clarke", "licenceClass": 2, "employmentStatus": "Active" },
  "vehicle": { "id": 10, "vehicleCode": "VEH-10", "registrationNumber": "CKL566", "vehicleType": "MediumRigid",
               "maxPayloadKg": 7000.00, "vehicleStatus": "Active" },
  "previousAssignmentId": 2, "previousAssignmentNumber": "DA-0002",
  "replacedByAssignmentId": null, "replacedByAssignmentNumber": null,
  "notes": null,
  "createdAt": "…", "createdBy": { "id": 2, "fullName": "Rachel Thompson" },
  "closedAt": null, "closedBy": null, "closeReason": null,
  "allowedActions": ["reassign", "cancel", "complete"]
}
```

- `version` changes every time the record is updated. Send it back in reassign, cancel and complete requests.
- `allowedActions` is a hint for the UI only. It is empty for closed assignments, and leaves out `reassign` once the order's deadline has passed. The server always validates actions independently.

### GET /assignments/{id}

`{ "assignment": Assignment, "history": Assignment[] }`. The history holds every assignment for the same order, oldest first, including this one.

### POST /assignments

```json
{ "orderId": 1, "driverId": 5, "vehicleId": 1, "notes": "optional, ≤1000 chars" }
```
`orderId`, `driverId` and `vehicleId` are required positive integers. Returns `201` with the Assignment object. See BUSINESS_RULES.md §4 for the checking order.

### POST /assignments/{id}/reassign

```json
{ "version": 812, "driverId": 3, "vehicleId": null, "reason": "Driver swap" }
```
- `version` and `reason` (≤500 characters) are required.
- Leave out `driverId` or `vehicleId`, or send `null`, to keep the current one.
- Returns `201` with `{ "previous": Assignment, "current": Assignment }`.

### POST /assignments/{id}/cancel

```json
{ "version": 812, "reason": "Customer postponed" }
```
`version` and `reason` (≤500 characters) are required. Returns `200` with the closed Assignment.

### POST /assignments/{id}/complete

```json
{ "version": 812, "reason": "optional completion note" }
```
Returns `200` with the closed Assignment.

### GET /driver/me (Driver role)

Returns `{ user, driver, message }`, the profile shown on the driver landing page.

### POST /dev/reset (Admin, Development environment only)

Deletes all data and restores the seed dataset. Returns the record counts. Outside the Development environment it returns 404.

---

## Example session (bash)

```bash
B=http://localhost:5080/api
TOKEN=$(curl -s -X POST $B/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"dispatcher@northline.test","password":"Northline!Dev2026"}' | jq -r .token)
H="Authorization: Bearer $TOKEN"

curl -s "$B/orders?eligibleOnly=true" -H "$H" | jq '.items[].orderNumber'
curl -s "$B/orders/4/candidates?vehicleId=3" -H "$H" | jq '.drivers[] | {code: .driver.driverCode, eligible, reasons: [.reasons[].code]}'
curl -s -X POST $B/assignments -H "$H" -H 'Content-Type: application/json' -d '{"orderId":4,"driverId":4,"vehicleId":3}' | jq
```
