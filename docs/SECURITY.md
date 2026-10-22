# Account access and data consistency

## Roles and sessions

Admin approves/rejects requests, assigns technicians, closes resolved tickets,
manages stores/contacts/manager accounts, and corrects recorded status mistakes.
StoreManagers create/edit tickets and progress/resolve work only in their assigned
store. Existing work remains accessible when a store is inactive.

A login returns a signed 30-minute JWT containing identity, role, store (for a
manager), and a session version. Each authenticated request checks the current
account in SQL. Missing/inactive accounts and outdated session/role/store claims
return 401. Deactivation/reactivation, password reset, email change and store
reassignment invalidate previous tokens. An already-running authorized operation
is not retroactively cancelled. Login rejects inactive accounts.

Passwords contain 8–128 characters and are hashed using ASP.NET Core's password
hasher. Admin sets initial/reset passwords explicitly; passwords/hashes are never
returned by manager-management responses. Email is normalized for uniqueness.
There is no public registration, emailed invitation or self-service password reset.

The frontend stores tokens only in tab memory. Logout/reload clears that local
session. Logout alone does not revoke a copied token; account changes invalidate
all previous sessions. Refresh tokens and per-session revocation remain deferred.

## Versioned writes

Ticket, store, technician and manager responses include an opaque `version`.
Editing, activation changes, password resets, ticket actions and corrections
must send it as `expectedVersion`. Create requests do not require an old version.
Missing versions return 400; stale versions or conflicting SQL rowversions return
409. Refresh and review current data before retrying. Never automatically repeat
an uncertain write.

Tracked ticket updates save changed fields rather than overwriting every column.
SQL rowversion checks protect against competing requests; workflow transactions
ensure ticket, assignment and history writes commit or roll back together.

## Correct status

Only Admin can reverse the latest recorded event. The request supplies its event
ID, current ticket version, and a required reason (maximum 2,000 characters).
The previous state comes from that recorded event, not a user-selected status.

Closed and rejected tickets cannot be edited directly. For an accidental closure
or rejection, Admin uses Correct status to return to the previous state, then
makes the correction through the normal workflow. The original event remains;
a new entry records the actor, UTC time, reason and reversed event ID. A
correction can itself be reversed, retaining the same audit trail.

Reversing an assignment to Approved withdraws the assignment without deleting
it. Reversing that correction restores the recorded assignment. These changes
are atomic. A withdrawn assignment is not presented as the current assignment.
Corrections do not silently rewrite ownership, historical actors or reasons.

## Database relationships and upgrades

Restrictive foreign keys protect ticket creators, assignment ticket/technician/
actor references, history ticket/actor references, correction links and recorded
assignments. Deactivation preserves references; no physical-delete UI exists.

The hardening migration checks orphan references and duplicate normalized emails
before changing the schema. Invalid legacy data stops migration with a diagnostic;
it is never automatically deleted. Existing accounts become active with session
version 1. Existing tokens without a session-version claim require a new login.
Back up a populated database and test migration against a restored copy first.

## Errors and login throttling

Errors use Problem Details with a `traceId`. Validation is 400, unauthenticated
requests 401, forbidden roles 403, missing/inaccessible tickets 404, conflicts 409,
login throttling 429 and unexpected failures 500. Internal exception details are
logged server-side and are not included in unexpected-error responses.

Login defaults to 20 attempts per remote IP per fixed 60-second window, with no
queue. A rejected attempt returns Retry-After; the frontend displays wait guidance.
Configure `LoginRateLimit:PermitLimit` and `LoginRateLimit:WindowSeconds` through
configuration. Forwarded IP headers are not trusted by default. Browser tests use
a larger test limit; a focused API test verifies the limiter independently.

Use HTTPS and a least-privilege database login when deploying. Public hosting,
account lockout, pagination and advanced infrastructure work remain separate from
this local portfolio release.
