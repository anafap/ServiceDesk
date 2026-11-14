# ServiceDesk interface redesign

The workspace uses a shared light theme with teal actions, distinct navigation icons,
compact tables, mobile record cards, accessible native dialogs, and consistent form
and feedback states. Workspace and Administration navigation are separate. Account
settings and sign-out live in the user menu.

## Page behavior

- Overview prioritizes role-scoped requests needing attention. Summary links open
  the corresponding ticket filters; Open requests includes approval, approved, and
  assigned tickets. The queue is ordered by priority, then oldest first, and shows
  up to eight requests across the user's authorized stores.
- Tickets supports debounced title search and URL-backed filters and pagination.
  New/edit forms open in a side panel (full screen on mobile), retain failed edits,
  and protect unsaved changes. Managers' assigned store is prefilled and read-only.
- Ticket details keeps the next permitted action near the description, groups
  Discussion/Photos/History into keyboard-accessible tabs, and puts status
  correction behind an Administrator actions disclosure. Posting comments or
  uploading photos preserves the active tab and refreshes the ticket version.
- Administration directories have search and active-state filters, grouped identity
  and contact information, mobile cards, and explicit operation-specific forms.
  Infrequent access/reset operations live in row action menus.
- Account settings separates editable name/phone details from Security. Email,
  role and store access are read-only. Profile updates preserve the current session;
  password changes invalidate existing sessions and return to sign-in with a notice.
- Dates are stored/interpreted as UTC and displayed locally. Appointment/history
  timestamps include the local timezone. Notifications have unread states and
  per-item pending feedback. Photos have selection previews and local validation.

## Additive API changes

- `GET /api/account` returns the authenticated user's `id`, `firstName`, `lastName`,
  `email`, `phoneNumber`, `role`, `storeId`, `storeName`, and `version`.
- `PUT /api/account/profile` accepts `firstName`, `lastName`, `phoneNumber`, and
  `expectedVersion`; it returns the updated account. It cannot change another
  account, credentials, role, email or store access. Existing rowversion checks
  protect against stale updates. No database migration is required for this redesign.
- `GET /api/dashboard` adds `attention: [{ ticket, action }]`. Admins see requests
  awaiting approval, approved requests needing assignment, and resolutions needing
  closure. Managers see assigned and in-progress work in their store.
- `GET /api/tickets?openOnly=true` limits results to open workflow stages. It
  combines with the existing title, status, priority and pagination parameters.

## Verification

Run the unit/API checks with the existing commands in the project README. SQL tests
use `SERVICEDESK_TEST_SQL_CONNECTION` and uniquely named disposable databases.

For deterministic interface checks against the running frontend:

```sh
E2E_BASE_URL=http://localhost:5173 npm --prefix frontend run test:browser -- redesign.spec.ts
```

`frontend/e2e/redesign.spec.ts` uses clearly fictional API fixtures to test layout,
responsive navigation, forms, profile updates, filters and activity tabs at 390,
768 and 1440 pixels. Its screenshots are in `docs/screenshots/redesign/`; they are
visual test fixtures, not evidence of a production deployment.

`python3 scripts/run-browser-tests.py` separately exercises the real API and database
with disposable accounts, stores and requests. It verifies both roles, the full
maintenance lifecycle, concurrency, account changes and access boundaries. It keeps
real-workflow screenshots in that run's temporary artifact directory.

Public password recovery, email changes, refresh/session management, SLA reporting
and hosting deployment remain separate work. Existing photo-service configuration
and deployment requirements still apply.
