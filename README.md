# ServiceDesk

A full-stack maintenance application for coordinating repairs across multiple
stores. StoreManagers report problems and verify repairs; Admins review requests,
schedule external technicians, and maintain store/contact records.

Built with **ASP.NET Core 10, C#, React, TypeScript, Vite, EF Core, and SQL Server**.

## Run it

Follow [the local setup guide](docs/SETUP.md) for prerequisites, Docker-hosted SQL,
private configuration, migrations, demo accounts, and startup commands. The API
and frontend run natively; the database has a repeatable Compose configuration.

Use [the portfolio demonstration](docs/DEMO.md) for the two-role walkthrough,
screenshot checklist, and interview talking points. Screenshots are pending
actual capture; no mock screens are presented as evidence.
[Learning notes](docs/LEARNING.md) explain the changes, concepts, interview
questions, and an independent exercise.

## Working features

- Shared login/logout with signed 30-minute JWTs and server-enforced roles.
- StoreManager dashboard/tickets scoped to the assigned store; Admin overview
  across all stores. One manager account belongs to one store.
- Create/edit tickets, title search, combined status/priority filters, result
  counts, dashboard summaries, and the five newest accessible tickets.
- Approval/rejection, technician assignment with a future visit, progress,
  resolution, closure, and status history with trusted actors and UTC timestamps.
- Named stores, categories, technicians, and users on ticket/assignment/history
  views. Missing references use explicit ID fallbacks; names reflect current data.
- Admin store/technician list, create, edit, deactivate, and reactivate screens.
- Loading, empty, error/retry, form-validation, and responsive workspace states.
- Explicit, idempotent Development demo provisioning with hashed passwords.

## Workflow and permissions

```text
AwaitingApproval → Approved → Assigned → InProgress → Resolved → Closed
        └──────→ Rejected
```

| Operation | Admin | StoreManager |
|---|---|---|
| Dashboard/list/detail/history/assignment | All stores | Assigned store only |
| Create/edit tickets | All stores | Assigned store only |
| Approve/reject/assign technician | Yes | No |
| Start progress / resolve | All stores | Assigned store only |
| Close resolved tickets | All stores | No |
| Manage stores and technicians | Yes | No |

The API checks store access before manager reads/writes. Missing and inaccessible
tickets return the same 404. Invalid transitions return 409; clients cannot skip
stages. Creator, assigning user, and history actors come from the validated JWT.
Technicians are external contacts without application accounts.

Deactivated stores accept no new tickets, but existing work remains accessible
and can finish. Inactive technicians cannot receive new assignments. Existing
assignments and history remain readable. Records are not physically deleted.

Open dashboard tickets include AwaitingApproval, Approved, and Assigned.
InProgress and Resolved have separate counts; Closed and Rejected contribute to
the total. Filtering/summaries currently use the complete authorized ticket list.

## Architecture

```text
frontend/ → HTTP → ServiceDesk.Api → ServiceDesk.Application → ServiceDesk.Domain
                       ↓                    ↑
                 ServiceDesk.Infrastructure → SQL Server
```

- **Domain:** entities, validation, and lifecycle methods; no EF/HTTP dependencies.
- **Application:** use cases and repository/transaction contracts.
- **Infrastructure:** EF mappings, repositories, batched name resolution, migrations,
  and SQL transaction implementation.
- **API:** authenticated endpoints, DTOs, validation responses, policies, and DI.
- **frontend/:** React pages, typed API clients, accessible forms, and derived views.
- **Tests:** domain/application checks, real JWT middleware/API authorization,
  frontend contract/behavior tests, and opt-in SQL integration tests.

Each workflow action shares one explicit transaction across ticket, assignment,
and history writes using the scoped DbContext. Failure rolls back all writes and
clears EF tracking. Create/edit remain single-save operations.

## API overview

- `POST /api/auth/login`: returns a bearer token, expiry, and safe user/store details.
- `GET/POST /api/tickets`, `GET/PUT /api/tickets/{id}`.
- `POST /api/tickets/{id}/{approve|reject|assign|progress|resolve|close}`.
- `GET /api/tickets/{id}/history` and `/assignment` (204 if no assignment).
- `GET /api/stores`, `/categories`, `/external-technicians` for active choices;
  technician directory access is Admin-only.
- Admin-only `/api/admin/stores` and `/api/admin/external-technicians`: GET lists
  including inactive records, POST create, PUT `/{id}` edit, PATCH `/{id}/status`
  with `{ "isActive": false }` or `true`.

Swagger in Development provides an Authorize control for bearer tokens. Display
responses contain names/IDs only where needed; no password hashes or signing keys.

## Verification

```sh
dotnet build
dotnet test
npm --prefix frontend test
npm --prefix frontend run lint
npm --prefix frontend run build
```

Latest feature checks: **110 backend tests passed with SQL enabled**, zero skips;
**59 frontend tests passed**; builds/lint passed. Database-free backend runs skip
SQL cases explicitly. Tests are verification evidence, not a replacement for
browser review of newly added screens. A clean local clone successfully restored
dependencies/tools, started the separate Compose SQL instance, applied migrations,
seeded demo data twice, and built the frontend. A real three-account HTTP
walkthrough verified management, inactive-record behavior, named responses, and
the full Admin/StoreManager workflow. The temporary review SQL instance was then
removed.

Privately set `SERVICEDESK_TEST_SQL_CONNECTION` to enable SQL tests. They override
the supplied database name with `ServiceDesk_TransactionTests_<guid>`, migrate a
fresh database, verify real commits/rollback/retry/administration/demo seeding,
and delete it afterward. The supplied database is not migrated or cleared.
Use a local login allowed to create/drop databases. Interrupted processes may
leave a test-prefixed database requiring manual cleanup.

## Security and limits

Secrets belong in user-secrets or environment configuration outside Git. Tokens
stay only in browser memory: reload, logout, expiry, or 401 requires sign-in.
Logout does not revoke copied tokens. Use HTTPS for hosted environments.

This release targets a local portfolio demo. Public deployment, user-management
screens/password reset, multi-store manager assignments, refresh/revoked tokens,
notifications, historical name snapshots, server-side pagination, and optimistic
concurrency are deferred. Transactions protect atomicity; they do not prevent
all conflicts between simultaneous requests.
