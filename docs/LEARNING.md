# Understand and explain this increment

## What changed and where

| Change | Significant code | Why it exists |
|---|---|---|
| Manager repair verification | `ServiceDeskEndpoints`, frontend `ticketWorkflow` rules, security tests | Managers can progress/resolve own-store tickets; only Admin can close. The server remains authoritative. |
| Administration | `AdministrationService`, `AdministrationRepository`, `AdministrationPage` | Separates validated business operations, SQL persistence, and interactive forms. |
| Readable references | `DisplayNameRepository`, ticket/session response DTOs, frontend display helpers | Resolves only authorized reference IDs, retaining history for inactive records. |
| Demo provisioning | `DemoDataSeeder`, SQL seed tests | Creates reproducible two-store data and hashed accounts without resetting passwords or duplicating edited tickets. |
| Repeatable setup | `compose.yaml`, local EF tool manifest, setup/demo guides | Lets a reviewer run the application without developer-specific configuration. |

## Concepts to understand

- A JWT proves identity and carries role/store claims; each API operation still
  checks what that identity may access. Hiding a React button is only usability.
- A database transaction groups writes. It does not solve every simultaneous
  update conflict; optimistic concurrency is a separate concern, now handled with SQL rowversions.
- Deactivation is a business state. It removes a record from new-work choices
  while preserving existing references and the ability to finish repairs.
- DTOs control the public contract. Adding display names is safer than sending
  full User entities or fetching an unrestricted directory in the browser.
- Batched ID lookups avoid a separate database request for every displayed row.
- React forms use controlled values and derive validation; failed submissions
  retain input. Reads can be cancelled; uncertain writes are not silently retried.
- Idempotent setup means re-running it preserves the intended state. A generated
  password is hashed when creating an account, not reapplied on every startup.
- Clean-checkout verification exposes missing dependencies/configuration hidden
  by local caches. Here it caught a vulnerable transitive cryptography package.

## Three interview questions

1. **How do you stop a manager changing a ticket in another store?**
   The API reads the ticket, compares its store to the validated identity's store,
   and returns 404 for inaccessible or missing tickets before invoking the use case.
2. **Why deactivate technicians instead of deleting them?**
   Existing assignments must remain understandable. New assignments require an
   active technician, while historical reads include inactive records.
3. **How do you know the setup and workflow are reproducible?**
   A clean clone restored dependencies, started a separate SQL container, migrated
   an empty database, seeded twice, and passed the real three-account HTTP
   walkthrough plus SQL transaction/administration/seed tests.

## Independent exercise

Using demo data, assign a technician to a ticket, deactivate that technician,
and verify two things yourself: the saved assignment still shows their name,
and they are absent from the next assignment form. Reactivate them afterward.
Explain which API checks enforce the behavior even if the browser UI is bypassed.

## Incremental commits

- `8b0c1be`: Allow store managers to verify repair progress and resolution.
- `5f8ecd1`: Add administration and named two-role ticket workspaces.
- `4917444`: Document reproducible SQL setup and two-role portfolio demo.
- `7b97156`: Patch API cryptography dependency found in clean restore.

These earlier checkpoints remain local. The hardening increment below adds real
headless-browser verification and actual screenshots; no desktop permission is required.

## Seven-finding hardening increment

Implemented record versions and partial tracked saves, account administration and
session invalidation, restrictive references, audited corrections/read-only
terminal tickets, centralized safe errors, login throttling and real Chromium tests.

Significant code:

- `VersionedEntity`, EF mappings and migration: optimistic concurrency and
  restrictive database relationships. `TicketService` keeps corrections atomic.
- `ManagerService`/repository/endpoints and the Admin page: validated account
  creation, assignment, activation and password reset. User updates rotate session
  versions; JWT validation checks current database account state.
- `ApiExceptionHandler`, version filter and login policy: safe error contracts,
  explicit stale-write handling and Retry-After feedback.
- `portfolio.spec.ts` and `run-browser-tests.py`: actual browser behavior on fresh
  isolated SQL data, plus real screenshots with no desktop permission dependency.

Three interview questions:

1. **Why does a transaction not prevent lost updates?** Two requests can read the
   same earlier state. A rowversion condition makes the stale writer fail, while
   the transaction rolls back its associated writes.
2. **How can you invalidate an already-issued JWT?** Include a session version
   and compare it with current account state on authenticated requests. Account
   changes increment that version, making earlier tokens invalid.
3. **How do you correct an accidental rejection without losing evidence?** Admin
   reverses the latest recorded event with a mandatory reason. A new history entry
   references the original, and any assignment changes share the transaction.

Independent exercise: open the same ticket in two Admin tabs. Save an edit in
one, attempt another edit in the other, and explain why the conflict protects the
first save. Refresh before retrying; do not weaken the expectedVersion check.
