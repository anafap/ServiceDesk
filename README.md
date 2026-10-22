# Service Desk and Maintenance Management System

A portfolio project for managing maintenance requests across a company with many stores. Store managers report problems, the central service desk reviews and approves them, and approved requests are assigned to external technicians.

## Project goals

- Support multiple stores and store managers.
- Track assets such as POS terminals, printers, routers, CCTV equipment, and air conditioners.
- Manage the complete maintenance-request lifecycle.
- Record assignments, status history, decisions, and resolution details.
- Notify store managers and external technicians through integrations such as WhatsApp.
- Demonstrate clean architecture, domain-driven business rules, SQL Server persistence, testing, and API design.

## Business workflow

```text
Store manager reports a problem
        ↓
Ticket enters AwaitingApproval
        ↓
Admin reviews the request
        ├── Rejects it
        └── Approves it
                ↓
        Admin assigns an external technician
                ↓
        Technician visit is scheduled
                ↓
        Ticket moves to InProgress
                ↓
        Technician resolves the issue
                ↓
        Ticket is closed after confirmation
```

Technicians are third-party contacts and do not log in to this application. They receive assignment details by phone, WhatsApp, or email. Application users are administrators and store managers.

## Ticket statuses

```text
AwaitingApproval → Approved → Assigned → InProgress → Resolved → Closed
                         └──→ Rejected
```

Status changes are controlled by domain methods and should be recorded in ticket status history.

## Architecture

The solution follows a clean-architecture style:

```text
ServiceDesk.Api
        ↓
ServiceDesk.Application
        ↓
ServiceDesk.Domain

ServiceDesk.Infrastructure
        ↓
ServiceDesk.Application and ServiceDesk.Domain
```

### Domain

Contains business entities, enums, and business rules. It must not depend on ASP.NET Core, Entity Framework, SQL Server, WhatsApp, or other technical concerns.

### Application

Contains use cases and orchestration, such as approving tickets, assigning technicians, creating history records, and requesting notifications. It defines interfaces for infrastructure services.

### Infrastructure

Contains technical implementations: Entity Framework Core, SQL Server, repositories, migrations, notification providers, and file storage.

### API

Contains HTTP endpoints, authentication configuration, request/response models, dependency injection, and HTTP status-code handling. API endpoints should remain thin and call Application use cases.

### Tests

Contains unit tests for domain rules and application tests for use cases. Security integration tests exercise the real API and JWT middleware with isolated
repository fakes. Opt-in SQL Server integration tests use disposable databases to
verify real workflow commits, partial-write rollback, and retry after failure.

## Current domain model

- `Store`: physical company location.
- `Asset`: equipment located at a store.
- `User`: application user with an Admin or StoreManager role.
- `ExternalTechnician`: third-party technician contact without login access.
- `SupportTicket`: maintenance request and controlled status workflow.
- `TechnicianAssignment`: assignment, schedule, notes, and assigning admin.
- `TicketStatusHistory`: audit record for every status change.

## Planned milestones

1. Complete and test the domain model.
2. Add Application use-case interfaces and services.
3. Add Entity Framework Core and SQL Server persistence.
4. Add repositories and database migrations.
5. Build ticket, store, asset, and assignment API endpoints.
6. Add authentication and role-based access for admins and store managers.
7. Add WhatsApp/email notification abstractions and provider integration.
8. Add preventive-maintenance scheduling, dashboards, reports, and audit views.
9. Add API integration tests, documentation, seed data, and deployment instructions.

## Current progress

The current implementation has completed the domain foundation, persistence wiring, and the first ticket workflow API slice.

- Domain entities and ticket lifecycle rules are implemented.
- Domain and application tests cover the main ticket workflow and status-history transitions.
- `TicketService` contains create, approve, reject, assign, progress, resolve, and close use cases.
- Ticket API endpoints exist for creation, listing, approval, rejection, assignment, progress, resolution, closing, detail lookup, and history retrieval.
- Ticket status history records the previous status, new status, actor, time, and reason for workflow transitions.
- `ServiceDeskDbContext` and EF Core entity configurations are present.
- `SupportTicketRepository`, `TechnicianAssignmentRepository`, and `TicketStatusHistoryRepository` are implemented.
- SQL Server connection configuration is supplied through user-secrets or environment variables.
- Dependency injection registers the database context, repositories, and `TicketService`.
- The backend suite passes 96 tests when SQL Server integration tests are enabled,
  including authentication, authorization, and atomic workflow persistence.
- React provides sign-in/sign-out, a real ticket list, a create-ticket form, and ticket details/history.
- JWT issuance, role checks, store-level access, and trusted audit identity are implemented.

Ticket editing and role/status-aware workflow screens are implemented, including
future technician assignment and saved visit details. Overview shows total, open,
in-progress, and resolved counts plus the five newest accessible tickets. Open
includes AwaitingApproval, Approved, and Assigned; Closed and Rejected contribute
only to the total. Tickets supports case-insensitive title search and combined
status/priority filters, result counts, and clearing filters. These views use the
complete authenticated ticket list; server-side filtering/pagination is deferred.
The frontend suite passes 53 tests; production build and lint pass.
Mrs.Ana confirmed the Admin browser flow for login, list/create, persistence,
mobile layout, and logout; the newer details/edit/workflow and dashboard/filter screens still need browser review.
See [frontend/README.md](frontend/README.md) for frontend commands and session behavior.

## Authentication and API access

Configure these values outside Git, using API user-secrets or environment variables:

- `ConnectionStrings:DefaultConnection`: the local SQL Server connection.
- `Jwt:Key`: a randomly generated signing key of at least 32 UTF-8 bytes.
- `Jwt:Issuer` and `Jwt:Audience`: matching values used for token issuance/validation.
- Development-only `DemoUser:Email` and `DemoUser:Password`: optional demo Admin
  initialization. Existing passwords are not reset automatically.

`POST /api/auth/login` accepts `{ "email": "...", "password": "..." }` and returns
`accessToken`, `tokenType` (`Bearer`), `expiresAt`, and public user metadata
(`id`, `name`, `role`, `storeId`). Passwords/hashes are never returned. Tokens
expire after 30 minutes. Invalid credentials return 401 with no user details.

Send `Authorization: Bearer <accessToken>` for data endpoints. Swagger in
Development includes an Authorize control for the returned token. Invalid,
expired, wrongly scoped, or tampered tokens return 401; wrong roles return 403.

| Operation | Admin | StoreManager |
|---|---|---|
| List/create tickets | All stores | Assigned store only |
| Read ticket details/history/assignment | All stores | Assigned store only |
| Edit title/description/priority | All stores | Assigned store only |
| Approve/reject/assign/progress/resolve | Allowed | Forbidden |
| Close resolved tickets | All stores | Assigned store only |
| Store lookup | All active stores | Assigned active store only |
| Categories | Allowed | Allowed |
| External technician contacts | Allowed | Forbidden |

Cross-store detail/history/close requests return 404 to avoid revealing ticket existence.
`GET /api/tickets/{ticketId}` returns the full description, status, priority,
store/category/creator IDs, optional asset ID, and creation time. The existing
`GET /api/tickets/{ticketId}/history` returns recorded transitions oldest first.
Empty history is shown honestly; creation events are not synthesized.
`PUT /api/tickets/{ticketId}` edits only title, description, and priority, with
required text/length/priority validation. It preserves ownership, creation time,
and lifecycle status. Editing does not create a status-transition history entry.

Creator, assigning Admin, and history actors come from the validated token;
client-supplied actor IDs are ignored. Create requests now accept title,
description, categoryId, storeId, and priority. Transition requests accept reason;
assignment also accepts externalTechnicianId, scheduledAt, and notes. Visit times
are accepted with a timezone offset and normalized to UTC; only active technician
IDs and future dates are accepted. `GET /api/tickets/{ticketId}/assignment` returns
the latest assignment, or 204 when none exists, with the same store access rules.

For a placeholder technician in a local demo database, run
[SeedDemoTechnician.sql](ServiceDesk.Infrastructure/Sql/SeedDemoTechnician.sql).
It is idempotent and creates a clearly labeled demo contact; no messages are sent.
This feature does not add technician contact-management screens or notifications.
Each workflow action wraps ticket, assignment (when applicable), and history
writes in one SQL transaction. A failure rolls all writes back; EF's change
tracker is cleared so subsequent reads do not return attempted changes.
All participating repositories share the request-scoped DbContext. Creation and
general editing remain single-save operations. Concurrent-request conflict
detection is separate from this atomicity guarantee.

The React session stays in memory and is cleared on reload/sign-out/expiry/401.
Logout does not revoke copied tokens; refresh tokens and revocation are not yet
implemented. StoreManager accounts require an assigned store and a hashed
password; account management is not exposed as a public API. Use HTTPS for any
hosted environment. This remains a portfolio demo, not a production deployment.

## Development commands

```bash
dotnet build
dotnet test
dotnet run --project ServiceDesk.Api
```

## SQL Server transaction tests

The default `dotnet test` run needs no database: 88 tests pass and the three SQL
fact/theory groups are explicitly skipped. To run all 96 cases, provide
`SERVICEDESK_TEST_SQL_CONNECTION` through your local environment or secret manager
and run `dotnet test`. Never commit its value.

These tests replace the supplied connection's database name with a unique
`ServiceDesk_TransactionTests_<guid>` name before opening a connection. They
apply the existing migrations, seed disposable test data, force SQL constraint
failures, verify persisted state using fresh DbContexts, and delete each test
database afterward. Use a local test SQL Server login with permission to create
and drop databases. The supplied database itself is never migrated or cleared.
Interrupted test processes may leave a database with this test-only prefix to
remove manually.

The rollback tests cover approve, reject, assign, start progress, resolve, and
close when history insertion fails; assignment insertion failure; successful
retry on the same context; and the complete successful workflow. They require
SQL Server rather than an in-memory fake to verify actual transaction behavior.

## Status

🚧 In development

See [`roadmap.local.md`](roadmap.local.md) for the learning roadmap and the remaining project flow.
