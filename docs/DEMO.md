# Portfolio demonstration

## Project description

ServiceDesk is a full-stack maintenance ticket application built with ASP.NET
Core, React, TypeScript, EF Core, and SQL Server. It models a multi-store repair
workflow with JWT authentication, server-enforced roles/store access, technician
scheduling, searchable tickets, dashboard summaries, and audit history. Workflow
writes use SQL transactions, verified with real rollback tests.

## Two-role walkthrough (about 4 minutes)

1. Sign in as Manager1. Point out the assigned store, scoped dashboard, and
   absence of administration navigation. Create a ticket with a clear title,
   category, and priority.
2. Sign in as Admin. Find the request using title/status/priority filters. Open
   its details, approve it, and assign an active technician with a future visit.
3. Sign in as Manager2. Confirm the other store's ticket is absent; its direct
   detail URL returns the unavailable/not-found view.
4. Return as Manager1. Start progress when work begins, mark resolved after
   checking the repair, then close the request. Explain that these transitions
   must happen in sequence and the API enforces the same rules as the UI.
5. Show named actors, technician, and UTC timestamps in assignment/history.
6. As Admin, create/edit a demo store and technician. Deactivate/reactivate them.
   Explain that inactive records are excluded from new work but retained in history.
7. Explain atomic saves: history or assignment failure rolls back the ticket
   change, and SQL integration tests verify persisted state using fresh contexts.

Use demo-only contacts and data. No notifications are sent. Do not show passwords,
JWTs, user-secrets, connection strings, or personal contact information on video.

## Screenshot checklist

Capture actual running screens, with demo records and readable names:

- `screenshots/admin-dashboard.png`: Admin summaries and recent tickets.
- `screenshots/ticket-filters.png`: combined filters and result count.
- `screenshots/ticket-history.png`: assignment and recorded history.
- `screenshots/manager-workspace.png`: assigned store and scoped actions.
- `screenshots/store-administration.png`: Admin list/form and activation controls.
- `screenshots/technician-administration.png`: technician management.

Only link files in the README after they have actually been captured and checked.
Browser verification from the earlier release does not automatically verify the
new administration and StoreManager screens. Record new checks in progress notes.

## Interview talking points

- React stores form/filter selections in state and derives filtered results.
- Authorization is enforced by the API, including store checks for manager writes.
- Domain methods enforce lifecycle rules; Application orchestrates use cases;
  Infrastructure implements SQL persistence and transactions.
- DTO display names avoid exposing user/contact directories to managers.
- Deactivation preserves references and historical records.
- Transactions prevent partial saves; optimistic concurrency remains separate work.

## Remaining limitations

This is a local portfolio application. Public hosting, multi-store manager
assignments, account-management UI, password reset, refresh/revoked tokens,
notifications, historical name snapshots, server-side ticket pagination, and
concurrent-update conflict handling are deferred. Names reflect current records.
Store managers still have access to existing work when their store is inactive.
