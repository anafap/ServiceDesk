# Store-confirmed technician visits

Admin chooses the technician and proposes a visit time. The store manager must
confirm that time and provide a contact name and phone before repair can start.
Technicians and store contacts do not need application accounts.

## Workflow and permissions

While the ticket remains **Assigned**, the assignment displays **Awaiting store
confirmation**, **Time declined**, or **Visit confirmed**. These are visit states;
the existing seven ticket statuses and numeric values are unchanged.

- Own-store manager: **Confirm visit**, with contact name and phone; or **Decline
  visit time**, with a reason. A declined time needs an Admin-proposed replacement.
- Admin: **Change visit time** before repair starts. A new time clears confirmation
  and current contact details, requiring fresh manager confirmation.
- Admin or own-store manager: **Start repair** once the current visit is confirmed.
  This records arrival/work starting and moves Assigned to InProgress. Confirmation
  alone does not mean the technician has arrived.
- Resolve and Admin-only close retain their existing rules. No contact editing
  after confirmation or rescheduling after repair starts is included.

Time proposals and confirmations require a future scheduled time. A confirmed
visit can start after its scheduled time. Names trim to 1–100 characters, phone
numbers to 1–50, and decline reasons to 1–2,000. Phone text supports international
formats; it is not an account identifier or a verified telephone number.

## API contracts

All requests below use POST, bearer authentication, JSON and the current ticket
`expectedVersion` plus `expectedAssignmentVersion` from the assignment GET.
Success returns 204. Refresh both records after success; do not retry uncertain
writes automatically. Missing versions/invalid fields return 400, stale versions
or invalid state return 409, inaccessible resources return 404, and wrong roles
return 403. Unauthenticated requests return 401.

| Route | Permission | Additional fields |
| --- | --- | --- |
| `/api/tickets/{id}/visit/confirm` | Own-store manager | `contactName`, `contactPhone` |
| `/api/tickets/{id}/visit/decline` | Own-store manager | `reason` |
| `/api/tickets/{id}/visit/reschedule` | Admin | `scheduledAt`, an offset-bearing future timestamp |
| `/api/tickets/{id}/progress` | Admin or own-store manager | Optional `reason`; now requires confirmed visit |

Example confirmation:

```json
{
  "expectedVersion": "<latest ticket version>",
  "expectedAssignmentVersion": "<latest assignment version>",
  "contactName": "Store visit contact",
  "contactPhone": "+973 3300 1234"
}
```

The existing assignment GET additionally returns `version`, `visitState`
(`AwaitingConfirmation`, `Confirmed`, `Declined`), `contactName`, `contactPhone`,
`confirmedByUserId`, `confirmedByName`, `confirmedAt`, `declineReason`, and `repairStartedAt`.
Identity and timestamps come from the server, never client-provided actor fields.
Admin cannot confirm on behalf of a store manager.

## Consistency, audit and migration

Assignment SQL rowversions protect visit data. A ticket visit revision also
advances the ticket rowversion, serializing visit decisions against ticket status
changes and corrections. Each transaction commits assignment changes, visit audit
entries and notifications together. Neither rescheduling nor starting repair can
silently win over the other's already-loaded snapshot.

`VisitEvents` retains append-only snapshots of confirmations, declines,
reschedules, repair starts and confirmation resets, including actor, UTC time,
proposed time and relevant contact/reason fields. These records are separate from
status history, so a time decline does not become the latest reversible status
event. Normal UI displays the current arrangement; historical snapshots remain in
SQL. In-app notifications use the existing active Admin/current-store manager
recipient rules and contain no contact previews. No email or technician message
is sent.

Withdrawing/restoring an assignment clears its current confirmation. Correcting
InProgress back to Assigned clears confirmation and the recorded repair-start
marker. Reversing that correction directly into InProgress is blocked: confirm
the visit again and use Start repair. The earlier audit records remain intact.

Migration `20261113113214_AddVisitConfirmation` adds visit columns, assignment
rowversions, the ticket revision and visit audit table. Existing Assigned tickets
need confirmation; existing InProgress/Resolved/Closed tickets may finish without
invented past approvals. Historical assignments display “Recorded before visit
confirmation” when appropriate. Downgrading removes newly recorded visit data.

Review the generated migration/SQL and the upgrade instructions in SETUP.md.
Apply it to an existing database only after a fresh backup and restored-copy
review. Normal startup does not apply migrations. Deploy the API/frontend together
with the new schema: old clients cannot start repair without assignment versions
and confirmation. The original local business database is outside this increment's
rollout; do not restart the updated API against its old schema before migration.

## Implementation and learning

| Area | Significant files | Purpose |
| --- | --- | --- |
| Business rules | `TechnicianAssignment`, `VisitEvent`, `SupportTicket` | Contact validation, readiness, immutable audit snapshots and revision guard |
| Orchestration/API | `TicketService`, `ServiceDeskEndpoints` | Trusted role/store boundary, atomic decisions, guarded repair start and notifications |
| SQL | `ServiceDeskDbContext`, assignment repository, additive migration | Rowversions, restrictive foreign keys, tracked audit persistence |
| UI | `TicketAssignment`, workflow client/helpers, details page | Role-specific visit forms, readable contact/readiness, deliberate repair start |
| Verification | `VisitTests`, `VisitSqlTests`, `VisitMigrationSqlTests`, `SecurityApiTests`, `coordination.spec.ts` | Domain boundaries, HTTP access, SQL rollback/races/restored upgrade, real browser flow |

Understand the distinction between **ticket lifecycle** and **appointment
readiness**: a confirmed appointment is still Assigned until work starts.
Optimistic concurrency prevents stale decisions; transactions prevent partial
writes. They solve different problems. The API establishes trusted caller access;
React controls guide the user but cannot grant permission.

Three interview questions:

1. **Why keep visit events separate from status history?** A reschedule changes an
   appointment without changing ticket status; keeping distinct audit records
   prevents it from breaking the latest-status correction rule.
2. **What if Admin reschedules while the manager starts repair?** Both operations
   use current ticket/assignment versions and a transaction. The stale operation
   fails and rolls back instead of starting an unconfirmed visit.
3. **Why clear contact details when time changes?** The person available for the
   old appointment may not be available for the new one. Fresh confirmation
   records responsibility for that specific arrangement.

Independent exercise: decline a proposed time, have Admin change it, confirm with
a contact, then change it again from Admin. Explain why Start repair disappears
and why a manager tab showing the earlier confirmation receives a conflict.

Suggested commit boundary: `Require store-confirmed visits before starting repairs`.
Include backend/domain/migration, frontend, relevant tests and documentation as a
coherent change after reviewing the existing uncommitted pilot/UI work separately.
No commit or push is automatic.


## Verification — November 13, 2026

- Backend: 155 tests passed with real SQL enabled, zero skipped; build passed with
  zero warnings/errors. Includes domain/access restrictions, missing/stale versions,
  audit/notification rollback, both reschedule/start write orders and corrections.
- Frontend: 75 unit tests, TypeScript/Vite production build and Oxlint passed.
- Chromium: 15 checks passed; one scanner-dependent photo check skipped because
  scanner/worker processing was not enabled for this visit-focused run. New real
  two-role visit walkthrough covers decline, reschedule, contact validation,
  preserved input after failure, reconfirmation, stale start rejection and Admin
  repair start; the portfolio flow covers manager repair start. Desktop/mobile
  screenshots were visually inspected, including corrected panel spacing.
- Migration: generated/reviewed additive SQL; migrated a COPY_ONLY backup-restored
  populated disposable database. Tickets, assignments and accounts were retained;
  no confirmation was fabricated. Test databases and the migration-test backup
  were cleaned up. Original local/business database was not migrated or modified.
- No commit/push. Original-database backup/review/apply and coordinated API restart
  remain the rollout boundary.

The initial new browser test exposed shared Admin-password mutation by an earlier
walkthrough and full-page navigation discarding this app's intentional in-memory
session. The test now runs before password-changing tests and uses in-app links.
These were verification setup fixes, not application authentication changes.


## Subsequent approved local rollout

The local assignment-loading error was traced to the new frontend running against
an older API process and schema. Following explicit approval, a fresh verified
recovery backup and a successful restored-copy review, the pending migration was
applied locally and the API restarted. Existing business/account values were
compared before and after and preserved. Authenticated reads and actual assignment
panels were verified for Admin and both configured managers. This completes the
local rollout boundary recorded above; other deployments still require their own
backup, migration review and coordinated API/frontend upgrade.


## Visible visit status on tickets

Assigned tickets now display their current visit state on desktop/mobile ticket
lists, dashboard cards/attention rows and ticket details: **Awaiting visit
confirmation**, **Visit time declined** (red), or **Visit confirmed** (green).
The list/detail/dashboard ticket DTO includes `visitState`, resolved from the
latest active assignment using one batched SQL query for already-authorized IDs.
Non-Assigned tickets keep their ordinary lifecycle labels. The Assigned filter
continues to include all these visit arrangements; numeric lifecycle values and
Open counts are unchanged. No migration is required for this display refinement.

Declined visits are prioritized in Admin's Needs attention list with **Change
visit time**. Managers see **Waiting for Admin**. Once Admin proposes another
time, the visible status becomes Awaiting visit confirmation and the declined
Admin attention item disappears. Existing visit notifications still apply.
Withdrawn/older assignment decisions cannot override the current proposal.

Significant changes: TicketQueryRepository supplies batched readiness and scoped
attention queries; TicketResponse exposes readiness; TicketStatusBadge and
`ticketStatusLabel` keep all ticket surfaces consistent. SQL/API/unit/browser
checks cover decline, reschedule, latest-active assignment and store isolation.

Learning: derive the visible status from the authoritative lifecycle plus current
visit state rather than storing a second conflicting ticket status. Three short
interview follow-ups: Why batch assignment reads? To avoid one query per ticket.
Why scope before enrichment? To prevent cross-store disclosure. Why preserve
lifecycle values? Visit readiness refines Assigned without changing transition
rules, existing stored statuses or aggregate definitions.

Independent exercise: decline a visit, check its red status on Tickets and Admin's
Needs attention, then reschedule and verify both displays update after refresh.
Suggested commit: `Show declined visit status across ticket views and Admin attention`.


Visible-status verification: 156 SQL-enabled backend tests, 76 frontend tests,
production frontend build/lint and 15 Chromium checks passed. One scanner-dependent
photo check was skipped. Local API restarted; actual declined-ticket labels were
verified read-only for Admin and manager accounts. No schema migration or original
ticket changes were needed for this display refinement.
