# Architecture

## Overview

```
┌────────────────────┐   /api (Vite proxy or nginx)   ┌──────────────────────────┐    EF Core / Npgsql   ┌──────────────┐
│ React SPA          │ ─────────────────────────────▶ │ ASP.NET Core Web API     │ ────────────────────▶ │ PostgreSQL 16│
│ (Vite, TS, shadcn) │ ◀───────── JSON / JWT ──────── │ (.NET 10, controllers)   │                       │ (+btree_gist)│
└────────────────────┘                                └──────────────────────────┘                       └──────────────┘
```

The frontend and backend are separate projects. The browser only ever calls `/api` on its own origin: in development Vite proxies it to the API, and in Docker nginx does.

## Repository layout

```
backend/
  Northline.sln
  src/Northline.Api/
    Controllers/       thin HTTP layer: routing, auth attributes, status codes
    Services/          DispatchService (all writes), *QueryService (reads), AuthService, SummaryService
    Validation/        DispatchRuleEvaluator (pure BR-01..07), LicenceRules + DispatchOptions (config)
    Data/              NorthlineDbContext, Configurations/, Migrations/, Seed/, DbConstraints
    Entities/          EF Core entities and enums
    Dtos/              request/response contracts, query objects, mapping
    Authorization/     policies, JWT options, ICurrentUser
    Common/            DomainException, ErrorCodes, problem responses, PostgresErrorTranslator, NzTime
  tests/Northline.Api.Tests/   developer tests (xUnit): Unit/ and Integration/ (Testcontainers)
frontend/
  src/
    auth/              AuthContext (JWT in sessionStorage), RequireAuth route guard
    components/        layout (AppShell, DriverShell), status badges, loading/empty/error states, shadcn ui/
    features/dispatch/ Dispatch Management: KPIs, board, assignments table, dialogs, drawer
    pages/             Login, Dashboard, placeholders, driver home, 403/404
    lib/               API client, DTO types, formatting (NZ time zone)
    modules.ts         navigation and placeholder module definitions
docs/                  business rules, API, seed data, data integrity, architecture
scripts/               reset-db.sh / reset-db.ps1
docker-compose.yml, .env.example
```

## Backend design

- **Controllers** do no business logic. They hand requests to services, and `DomainException`s turn into problem responses through `DomainExceptionHandler`.
- **DispatchRuleEvaluator** is a pure class. It receives entities and the relevant active assignments and returns every violation. It has no database access, which makes it easy to unit-test. The same evaluator powers both validation and the advisory `/candidates` view, so they cannot disagree.
- **DispatchService** owns transactions, row locks, version checks, state transitions and audit fields. See [DATA_INTEGRITY.md](DATA_INTEGRITY.md).
- **Query services** are read-only and use `AsNoTracking`. Driver and vehicle availability is worked out at read time from status plus active assignments.
- **Configuration** is bound and validated at startup: `Jwt` (signing key at least 32 characters), `Dispatch:MinimumLicenceClass` (all vehicle types, 1–5), `ConnectionStrings:Northline`, `Seed:DevPassword`, `Database:MigrateOnStartup` / `SeedOnStartup`.
- **Time**: `TimeProvider` is injected everywhere, so tests use a fixed clock. Data is stored as `timestamptz` in UTC and shown in `Pacific/Auckland` time with the `en-NZ` culture, whatever the server's locale.
- **Authorization**: a fallback policy requires authentication on every endpoint unless marked `[AllowAnonymous]`. `DispatchRead` and `DispatchWrite` allow Admin and Dispatcher; `DriverOnly` and `AdminOnly` cover the rest. Unauthenticated requests get 401 and the wrong role gets 403, both as problem responses.
- **Passwords** are hashed with ASP.NET Core Identity's `PasswordHasher<T>` (PBKDF2). There is no ASP.NET Identity user store.
- **Startup**: the API applies pending migrations and seeds an empty database (both configurable). `dotnet run -- --reset-db` re-seeds and exits.

## Frontend design

- **Routing** (React Router): `/login`, `/dashboard`, `/dispatch`, the six placeholder routes, `/driver`, and a 404 page. Route guards mirror the API's role rules, so a Driver opening `/dispatch` sees the 403 page. The API enforces the rules independently.
- **Data** is fetched with TanStack Query. After every dispatch action, all affected data is refreshed (summary, orders, drivers, vehicles, assignments, candidates). It is also refreshed after a 409 or 422, so the screen reflects the server state.
- **Dispatch page**:
  - KPI cards.
  - **Dispatch board** tab: pending orders with search, scope, priority filter and sort, next to a Resources panel of drivers and vehicles with their derived availability.
  - **Assignments** tab: filter, search, sort and paging.
  - The open assignment and the tab are kept in the URL (`?tab=assignments&assignment=3`).
- **Assign and reassign dialogs**: choose an order, then a vehicle and driver from `/candidates`. Ineligible options are shown with their reasons and cannot be selected. A confirmation step follows, and errors returned by the server are shown in the dialog.
- **Assignment drawer**: details, audit fields, reassignment history and the reassign, complete and cancel actions, each behind a confirmation dialog that sends the record's `version`.
- **Test hooks**: interactive and key display elements carry stable `data-testid` attributes for the future QA automation project. No tests are written here.
