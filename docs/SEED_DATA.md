# Seed Data — Initial Application State

The development dataset is defined in `backend/src/Northline.Api/Data/Seed/SeedData.cs`. It is built so that every important positive and negative dispatch scenario can be triggered straight away, with no extra setup.

## How dates work

All seeded dates are **relative to the moment of seeding**, in New Zealand local time (`Pacific/Auckland`). This keeps the data from going stale.

- **D** = the seeding day (NZ local). **D+1 08:00** = tomorrow at 08:00 NZ time. **D-1** = yesterday.
- **N** = the moment of seeding, rounded down to the nearest 15 minutes (used for the in-progress job).

Times in the API are returned in UTC (ISO 8601). The UI and error messages show NZ time.

> **Reset before each test session.** As real time passes, the relative state drifts. For example, the in-progress job DA-0004 ends at N+4h, and tomorrow's orders expire once tomorrow has passed. A reset re-anchors every date to the new "now". See [Reset](#reset).

## Accounts

All seeded accounts share one **development-only** password, set by `Seed:DevPassword` (default in `appsettings.Development.json` / `.env.example`: `Northline!Dev2026`).

| User ID | Email | Name | Role | Notes |
|---|---|---|---|---|
| 1 | `admin@northline.test` | Alex Morgan | Admin | Full access, plus `POST /api/dev/reset` |
| 2 | `dispatcher@northline.test` | Rachel Thompson | Dispatcher | Created most seeded assignments |
| 3 | `dispatcher2@northline.test` | Tama Rangi | Dispatcher | Second dispatcher, for trying conflicting edits from two browser tabs or sessions |
| 4 | `driver@northline.test` | Mere Tawhiri | Driver | Linked to DRV-005; landing page only |

The UI keeps each sign-in in `sessionStorage`, so two tabs can be signed in as different users at the same time.

## Drivers (10)

Database IDs 1–10 follow the code order.

| ID | Code | Name | Licence class | Employment status | Seeded schedule / purpose |
|---|---|---|---|---|---|
| 1 | DRV-001 | Aroha Ngata | 5 | Active | DA-0007 (D+3 07:00–12:00). Free on D+1 and D+2. |
| 2 | DRV-002 | James Wilson | 5 | Active | **DA-0001 (D+1 06:00–14:00)**: overlap scenario |
| 3 | DRV-003 | Priya Sharma | 4 | Active | Free. Completed DA-0005 in the past. Heavy Rigid OK, Heavy Combination ✗ |
| 4 | DRV-004 | Liam O'Connor | 2 | Active | Free. Was on DA-0002 (Reassigned), which no longer blocks |
| 5 | DRV-005 | Mere Tawhiri | 1 | Active | Free. Light Commercial only. Linked to the driver login |
| 6 | DRV-006 | Daniel Chen | 5 | **OnLeave** | Inactive-resource scenario (BR-02) |
| 7 | DRV-007 | Sione Taufa | 4 | **Inactive** | Inactive-resource scenario (BR-02) |
| 8 | DRV-008 | Emma Clarke | 2 | Active | **DA-0003 (D+2 07:00–13:00)**: current end of the reassignment chain |
| 9 | DRV-009 | Hemi Walker | 5 | Active | **DA-0004 (N-2h to N+4h)**: on a job now |
| 10 | DRV-010 | Sophie Martin | 1 | Active | Had DA-0006 (Cancelled) in TO-1014's window; free for it again |

## Vehicles (10)

Database IDs 1–10 follow the code order.

| ID | Code | Registration | Type | Max payload | Status | Seeded schedule / purpose |
|---|---|---|---|---|---|---|
| 1 | VEH-01 | KXR482 | Light Commercial | 1,200 kg | Active | Was on DA-0006 (Cancelled) |
| 2 | VEH-02 | LTM205 | Light Commercial | 1,500 kg | Active | Free |
| 3 | VEH-03 | HJW771 | Medium Rigid | 6,000 kg | Active | DA-0007 (D+3 07:00–12:00). Was on DA-0002 (Reassigned) |
| 4 | VEH-04 | MBD319 | Medium Rigid | 8,000 kg | **Maintenance** | BR-03 scenario |
| 5 | VEH-05 | JRT604 | Heavy Rigid | 12,000 kg | Active | **DA-0001 (D+1 06:00–14:00)**: overlap scenario |
| 6 | VEH-06 | PKE118 | Heavy Rigid | 14,000 kg | Active | Free. Completed DA-0005 in the past |
| 7 | VEH-07 | NZH952 | Heavy Combination | 25,000 kg | Active | Free. Largest active vehicle |
| 8 | VEH-08 | GHT427 | Heavy Combination | 28,000 kg | **Inactive** | BR-03 scenario |
| 9 | VEH-09 | RWA830 | Heavy Rigid | 12,000 kg | Active | **DA-0004 (N-2h to N+4h)**: on a job now |
| 10 | VEH-10 | CKL566 | Medium Rigid | 7,000 kg | Active | DA-0003 (D+2 07:00–13:00) |

Minimum licence class by type: Light Commercial 1, Medium Rigid 2, Heavy Rigid 4, Heavy Combination 5 (see [BUSINESS_RULES.md](BUSINESS_RULES.md#br-07-driver-qualification-simplified-licence-rules)).

## Transport orders (17)

Database IDs 1–17 follow the order-number order (TO-1001 = 1 … TO-1017 = 17).

| ID | Order | Customer | Route | Cargo | Weight | Window (NZ) | Priority | Status | Eligible? |
|---|---|---|---|---|---|---|---|---|---|
| 1 | TO-1001 | Harbourside Retail Group | Penrose, Auckland → Wiri, Manukau | Boxed retail stock, 4 pallets | 900 kg | D+1 08:00–12:00 | High | Pending | ✓ |
| 2 | TO-1002 | Southern Cross Beverages | East Tamaki, Auckland → Mount Maunganui, Tauranga | Bottled beverages, 20 pallets | 22,000 kg | D+1 07:00–15:00 | Urgent | Pending | ✓ |
| 3 | TO-1003 | Pokeno Dairy Co-operative | Pokeno → Te Rapa, Hamilton | Chilled dairy products | 10,000 kg | D+1 10:00–13:00 | Normal | Pending | ✓ |
| 4 | TO-1004 | Waikato Building Supplies | Te Rapa, Hamilton → Cambridge | Timber framing and plasterboard | 5,000 kg | D+2 09:00–13:00 | Low | Pending | ✓ |
| 5 | TO-1005 | Northern Steel Fabricators | Wiri, Manukau → Te Rapa, Hamilton | Structural steel beams | 30,000 kg | D+2 06:00–14:00 | High | Pending | ✓ (but no vehicle fits) |
| 6 | TO-1006 | Tamaki Pharmaceuticals | East Tamaki, Auckland → Te Rapa, Hamilton | Pharmaceutical supplies | 1,100 kg | D-1 08:00–12:00 | Urgent | Pending | ✗ `ORDER_EXPIRED` |
| 7 | TO-1007 | Greenlane Office Furniture | Auckland CBD → Mount Maunganui, Tauranga | Office furniture | 3,500 kg | D+2 08:00–16:00 | Normal | **OnHold** | ✗ `ORDER_NOT_PENDING` |
| 8 | TO-1008 | Manukau Electrical Wholesale | Wiri, Manukau → Pokeno | Electrical cable drums | 2,400 kg | pickup D+3 15:00, deadline D+3 09:00 | Normal | Pending | ✗ `ORDER_INVALID_TIME_WINDOW` |
| 9 | TO-1009 | Kauri Coast Foods | Penrose, Auckland → Te Rapa, Hamilton | Packaged dry goods | 9,500 kg | D+1 06:00–14:00 | High | Assigned (DA-0001) | ✗ `ORDER_ALREADY_ASSIGNED` |
| 10 | TO-1010 | Bay of Plenty Kiwifruit Packers | Mount Maunganui, Tauranga → Penrose, Auckland | Kiwifruit trays | 4,200 kg | D+2 07:00–13:00 | Normal | Assigned (DA-0003) | ✗ |
| 11 | TO-1011 | Southern Cross Beverages | East Tamaki, Auckland → Pokeno | Mixed beverages | 11,000 kg | N-2h to N+4h | Normal | Assigned (DA-0004) | ✗ |
| 12 | TO-1012 | Harbourside Retail Group | Wiri, Manukau → Cambridge | Seasonal stock | 6,500 kg | D-2 07:00–12:00 | Normal | **Completed** | ✗ |
| 13 | TO-1013 | Cambridge Equine Feeds | Cambridge → Penrose, Auckland | Bagged horse feed | 3,000 kg | D+3 08:00–12:00 | Low | **Cancelled** | ✗ |
| 14 | TO-1014 | Tamaki Pharmaceuticals | East Tamaki, Auckland → Wiri, Manukau | Medical consumables | 800 kg | D+2 10:00–14:00 | High | Pending (DA-0006 was cancelled) | ✓ |
| 15 | TO-1015 | Kauri Coast Foods | Te Rapa, Hamilton → Mount Maunganui, Tauranga | Packaged dry goods (return leg) | 9,000 kg | D+1 14:00–18:00 | Normal | Pending | ✓ |
| 16 | TO-1016 | Waikato Building Supplies | Penrose, Auckland → Cambridge | Roofing iron | 4,800 kg | D+3 07:00–12:00 | Normal | Assigned (DA-0007) | ✗ |
| 17 | TO-1017 | Pokeno Dairy Co-operative | Pokeno → Penrose, Auckland | Milk powder, 25 kg bags | 12,500 kg | D+4 06:00–12:00 | Normal | Pending | ✓ |

## Dispatch assignments (7)

Database IDs 1–7 match the DA numbers. The next assignment created is **DA-0008**.

| ID | Assignment | Order | Driver | Vehicle | Window | Status | Created by | Closed | Close reason |
|---|---|---|---|---|---|---|---|---|---|
| 1 | DA-0001 | TO-1009 | DRV-002 | VEH-05 | D+1 06:00–14:00 | **Assigned** | Rachel Thompson | — | — |
| 2 | DA-0002 | TO-1010 | DRV-004 | VEH-03 | D+2 07:00–13:00 | **Reassigned** | Rachel Thompson | N-1 day, Rachel Thompson | "Driver requested a schedule change." |
| 3 | DA-0003 | TO-1010 | DRV-008 | VEH-10 | D+2 07:00–13:00 | **Assigned** (previous: DA-0002) | Rachel Thompson | — | — |
| 4 | DA-0004 | TO-1011 | DRV-009 | VEH-09 | N-2h to N+4h | **Assigned** (in progress) | Rachel Thompson | — | — |
| 5 | DA-0005 | TO-1012 | DRV-003 | VEH-06 | D-2 07:00–12:00 | **Completed** | Rachel Thompson | 30 min before deadline, Rachel Thompson | "Delivered on time." |
| 6 | DA-0006 | TO-1014 | DRV-010 | VEH-01 | D+2 10:00–14:00 | **Cancelled** | Alex Morgan | N-1 day, Alex Morgan | "Booked against the wrong customer account." |
| 7 | DA-0007 | TO-1016 | DRV-001 | VEH-03 | D+3 07:00–12:00 | **Assigned** | Tama Rangi | — | — |

DA-0004 has the note "Driver to call the Pokeno depot 30 minutes before arrival."

## Initial KPI values

Immediately after a reset (`GET /api/dispatch/summary`):

| KPI | Value | Explanation |
|---|---|---|
| Pending orders | 10 | TO-1001–1006, 1008, 1014, 1015, 1017 |
| Eligible pending orders | 8 | Pending minus TO-1006 (expired) and TO-1008 (invalid window) |
| Urgent pending (not expired) | 1 | TO-1002 |
| Assigned jobs | 4 | DA-0001, DA-0003, DA-0004, DA-0007 |
| Available drivers | 7 of 8 active (10 total) | DRV-009 is on DA-0004 now |
| Available vehicles | 7 of 8 active (10 total) | VEH-09 is on DA-0004 now |

"Available" means available **right now**. Once DA-0004's window ends (N+4h), both figures become 8.

## Scenario matrix

Use these records to reach each business condition. IDs refer to the tables above.

| Scenario | Rule | How to trigger | Expected result |
|---|---|---|---|
| Happy path | BR-08 | TO-1001 + DRV-005 + VEH-01 | 201, DA-0008, TO-1001 → Assigned |
| Only one vehicle fits | BR-04 | TO-1002 candidates | Only VEH-07 eligible. Pair it with DRV-001 or DRV-009 (class 5) |
| No vehicle fits | BR-04 | TO-1005 + any vehicle | 422 `VEHICLE_CAPACITY_EXCEEDED` (VEH-08 is also inactive) |
| Capacity boundary | BR-04 | none seeded at exact equality | covered by developer unit tests |
| Licence incompatible | BR-07 | TO-1004 + DRV-005 (class 1) + VEH-03 (Medium Rigid) | 422 `DRIVER_LICENCE_INCOMPATIBLE` |
| Licence OK | BR-07 | TO-1004 + DRV-004 (class 2) + VEH-03 | 201 |
| Driver overlap | BR-05 | TO-1003 + DRV-002 + VEH-06 | 422 `DRIVER_SCHEDULE_CONFLICT` (DA-0001) |
| Vehicle overlap | BR-06 | TO-1003 + DRV-003 + VEH-05 | 422 `VEHICLE_SCHEDULE_CONFLICT` (DA-0001) |
| Both overlap | BR-05/06 | TO-1003 + DRV-002 + VEH-05 | 422 with both violations |
| Back-to-back boundary (allowed) | BR-05/06 | TO-1015 + DRV-002 + VEH-05 | 201: TO-1015 starts exactly when DA-0001 ends |
| Driver on leave / inactive | BR-02 | any eligible order + DRV-006 / DRV-007 | 422 `DRIVER_NOT_ACTIVE` |
| Vehicle in maintenance / inactive | BR-03 | any eligible order + VEH-04 / VEH-08 | 422 `VEHICLE_NOT_ACTIVE` |
| Several failures at once | BR-09 | TO-1001 + DRV-007 + VEH-08 | 422 listing BR-02, BR-03, BR-07 |
| Expired order | BR-01 | TO-1006 | 422 `ORDER_EXPIRED` |
| On-hold order | BR-01 | TO-1007 | 422 `ORDER_NOT_PENDING` |
| Invalid time window | BR-01 | TO-1008 | 422 `ORDER_INVALID_TIME_WINDOW` |
| Completed / cancelled order | BR-01 | TO-1012 / TO-1013 | 422 `ORDER_NOT_PENDING` |
| Duplicate assignment | BR-10 | TO-1009 + any resources | 409 `ORDER_ALREADY_ASSIGNED` |
| Cancelled assignment doesn't block | BR-12 | TO-1014 + DRV-010 + VEH-01 | 201 |
| Reassigned assignment doesn't block | BR-11 | TO-1004 + DRV-004 + VEH-03 (DA-0002 was in an overlapping window) | 201 |
| Reassign driver | BR-11 | DA-0007 → DRV-003 | 201. DA-0007 Reassigned, DA-0008 Assigned with `previousAssignmentId` = 7 |
| Reassign vehicle only (same driver) | BR-11 | DA-0001 → VEH-06 | 201: the replaced assignment is left out of the conflict checks |
| Reassign with no change | BR-11 | DA-0007, same driver and vehicle | 422 `REASSIGN_NO_CHANGE` |
| Reassign into a conflict | BR-11 + BR-05 | Two steps: first assign TO-1001 + DRV-001 + VEH-02 (D+1 08:00–12:00), then reassign DA-0001 to DRV-001 | 422 `DRIVER_SCHEDULE_CONFLICT`; DA-0001 unchanged |
| Reassign to inactive driver | BR-11 | DA-0003 → DRV-006 | 422 `DRIVER_NOT_ACTIVE` |
| Existing reassignment chain | BR-11 | GET /assignments/3 | History: DA-0002 (Reassigned) → DA-0003 (Assigned) |
| Cancel | BR-12 | DA-0001 or DA-0007 | 200. TO-1009 / TO-1016 → Pending; resources freed |
| Cancel frees the window | BR-12 | Cancel DA-0001, then TO-1003 + DRV-002 + VEH-05 | 201 |
| Complete | Lifecycle | DA-0004 (in progress) | 200, TO-1011 → Completed |
| Change a closed assignment | Lifecycle | cancel / reassign / complete DA-0002, DA-0005 or DA-0006 | 409 `INVALID_STATUS_TRANSITION` |
| Stale version | Concurrency | cancel DA-0007 with an old `version` | 409 `ASSIGNMENT_MODIFIED` |
| Two dispatchers, one assignment | Concurrency | `dispatcher@` and `dispatcher2@` open DA-0007 in two tabs; one cancels, the other then reassigns | Second action: 409 `ASSIGNMENT_MODIFIED` |
| Driver role blocked | Roles | `driver@` calls any `/api/orders`, `/api/assignments` … | 403 `FORBIDDEN` |
| Not signed in | Roles | any protected endpoint without a token | 401 `UNAUTHORIZED` |

Note on `/candidates`: driver licence compatibility (BR-07) depends on the vehicle. It is only evaluated once a vehicle is selected (`vehicleId`), and the same applies to vehicles with `driverId`. The UI passes both automatically as you select.

## Reset

The reset deletes **all** data (including anything created during testing) and restores exactly this dataset, with dates re-anchored to the current time.

| Method | Command |
|---|---|
| API (Development environment, Admin token) | `POST /api/dev/reset` |
| Script, local source | `./scripts/reset-db.sh` or `./scripts/reset-db.ps1` |
| Script, Docker stack | `./scripts/reset-db.sh docker` or `./scripts/reset-db.ps1 docker` |
| Rebuild from scratch | `docker compose down -v && docker compose up -d` (the API migrates and seeds an empty database on startup) |

Seeded record IDs are deterministic, so existing JWTs stay valid after a reset.
