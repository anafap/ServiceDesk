# ServiceDesk frontend

React + TypeScript with Vite, React Router, and responsive CSS. The ASP.NET Core
solution remains at the repository root; browser code accesses it over HTTP.

## Run and verify

Follow [the full local setup](../docs/SETUP.md) for SQL, API configuration,
migrations and demo accounts. Then from the repository root:

```sh
npm --prefix frontend ci
npm --prefix frontend run dev
npm --prefix frontend test
npm --prefix frontend run lint
npm --prefix frontend run build
```

Vite normally serves http://127.0.0.1:5173 and proxies `/api` to the API on 5200.
The build type-checks before writing `dist/`. Preview serves static production
assets without the development API proxy. Production hosts need SPA fallback
routing and a separately configured API route.

## Workspace and workflows

- One login for Admin and StoreManager accounts; header identifies role/store.
- Overview shows accessible ticket totals/open/in-progress/resolved and recent
  tickets. Open includes awaiting approval, approved and assigned states.
- Tickets provides title search, combined exact status/priority filters, counts,
  clear filters, and creation using active stores/categories.
- Details supports title/description/priority editing and role/status-aware
  lifecycle actions, plus saved technician assignment and chronological history.
- Admins alone approve/reject/assign/close; both roles can progress/resolve, with
  manager access limited to the assigned store by the API.
- Admin-only Stores and Technicians pages provide list/create/edit/deactivate/
  reactivate. StoreManagers additionally supports account creation, editing,
  reassignment and password resets. No physical deletion or technician accounts.
- Names appear for store, category, creator, technician and audit actors, with
  explicit ID fallbacks. Inactive references remain readable; names are current,
  not historical snapshots. Manager views do not load unrestricted directories.

Deactivated stores block new tickets but existing work can finish. Inactive
technicians are not offered for new assignment. Past assignments remain visible.
Dates without a SQL timezone suffix are interpreted as UTC. Scheduling takes a
browser-local time and submits an ISO UTC timestamp.

Forms validate required values/lengths, preserve failed input and prevent pending
resubmission. Uncertain writes are not retried automatically; refresh before
resubmitting. Data pages provide loading/empty/error/retry states and cancel
obsolete reads. Ticket filters survive refresh while the page stays mounted.
Filtering and summaries currently derive from the full authorized API response.

## Authentication and tests

The bearer token stays only in tab memory. Reload, sign-out, expiry or 401 clears
the session; 403 preserves it. API permissions remain authoritative. Logout does
not revoke copied tokens by itself. Account changes invalidate previous sessions.
Admin password reset is implemented; public recovery and refresh tokens are deferred.
No database credentials or JWT signing keys belong in frontend configuration.

`npm test` uses Node's built-in TypeScript support (22.18+ or compatible newer
release), for unit tests. Playwright is a separate development dependency for browser tests.
Latest checks: 64 passing unit tests and two Chromium walkthroughs,
production build and lint. Tests cover API contracts, authentication, form and
workflow validation, filtering/summaries, UTC ordering, administration requests,
safe display names, versions, manager contracts and rate-limit guidance. Browser
checks cover real form interactions, both roles, stale-write messages, manager
session invalidation, Admin-only closure/correction, and mobile layout.

Existing-record writes include `expectedVersion` from the last loaded record.
409 conflicts preserve input and ask for refresh instead of silently overwriting.
Closed/rejected tickets disable editing; Admin uses Correct status with a required
reason to reverse the latest recorded event and preserve its history.

Use [the demo guide](../docs/DEMO.md), [security behavior](../docs/SECURITY.md), and
[setup](../docs/SETUP.md) for browser-test execution and actual screenshot captures.
The browser runner uses isolated headless Chromium and a disposable database,
without requiring desktop computer-use permission.
