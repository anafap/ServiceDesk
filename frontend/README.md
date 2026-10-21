# ServiceDesk frontend

React + TypeScript, built with Vite. React Router provides Overview and Tickets
pages. Modern CSS styles the shared responsive workspace layout.

## Local development

From this directory:

```bash
npm ci
npm run dev
```

Open the URL printed by Vite (normally http://localhost:5173).

```bash
npm run lint
npm test
npm run build
npm run preview
```

The build checks TypeScript before generating production assets in `dist/`.
Preview serves that build locally. A production host must route frontend paths
such as `/tickets` to `index.html` for browser routing.

## Current scope

The Tickets page loads real tickets from `GET /api/tickets`, with status and
priority labels, UTC dates, refresh, loading, empty, error, and retry states.
Title search is case-insensitive and ignores surrounding whitespace. Status and
priority filters combine with the title search using AND. Clear filters restores
the full accessible list; refresh preserves selections. Filtering runs locally
on the complete API response; server-side filtering/pagination is deferred.
It cancels requests on navigation and times out requests after 15 seconds.
The New ticket form loads real stores/categories, validates required fields and
text limits, creates a request through `POST /api/tickets`, and refreshes the
list after success. Fields are preserved on failure, and duplicate submissions
are blocked while saving. An interrupted write is never retried automatically:
check the ticket list before resubmitting an unconfirmed save.
Click a ticket title to open `/tickets/{ticketId}`. The detail page loads the
full description, status/priority, store/category/creator IDs, optional asset ID,
and UTC creation time. Its history timeline shows recorded status transitions,
actor IDs, timestamps, and reasons in chronological order. Store/user/category
names are not resolved; IDs are clearly labeled. Empty history is not replaced
with invented events.

Details and history have separate loading/error/retry states, so a history
failure does not hide loaded details. Missing or inaccessible tickets share the
same not-found view. Navigation cancels obsolete requests; dates without a
SQL timezone suffix are interpreted as UTC.

The detail page includes Edit ticket for title, description, and priority, with
validation, save/cancel, error preservation, and a fresh detail read after save.
Ownership and status changes are excluded from the edit form.

The Workflow panel offers actions based on the current role and ticket status:
approve/reject, assign, start progress, resolve, and close. Each action uses a
confirmation form with an optional reason. Admins control the workflow; managers
can close resolved tickets for their own store. Server rules remain authoritative.

Assignment loads active technicians from the real API, requires a future visit
time entered in the browser's local timezone, and sends an ISO UTC timestamp.
The saved assignment panel shows the technician ID, UTC visit time, assigning
user ID, and notes. Its data is reloaded from the API after changes and navigation.
Empty/failed technician lookups disable assignment and provide clear feedback.
A placeholder local contact can be provisioned using the backend's
`ServiceDesk.Infrastructure/Sql/SeedDemoTechnician.sql` script.

Workflow failures preserve form input. Conflict responses ask the user to refresh;
uncertain writes are never retried automatically. Edit/refresh controls are disabled
while a workflow write is pending. Successful writes reload details, assignment,
and history from the backend. No notification provider is invoked.

Overview summaries are not implemented yet. Login/logout and authenticated API
requests are implemented for Admin and StoreManager accounts.
No simulated ticket data or authentication is presented in the application.

Run the API separately from the repository root:

```bash
dotnet run --project ServiceDesk.Api --launch-profile http
```

SQL Server and the existing API user-secrets configuration must be available.
Vite forwards `/api` requests to `http://localhost:5200` during development,
so no backend CORS changes are needed. Restart Vite after proxy configuration
changes if it does not restart automatically. The proxy is development-only;
production hosting must route `/api` to the backend separately. Do not use
the frontend preview server to verify API integration.

## Sign-in and session behavior

Sign in with an existing account. In Development, the API can initialize the
configured demo Admin from `DemoUser:Email` and `DemoUser:Password` user-secrets;
credentials are never included in frontend source. StoreManager accounts must
already have a hashed password and an assigned store in the database.

The frontend treats the access token as opaque and attaches it as a bearer
Authorization header. The token lives only in tab memory, not localStorage or
sessionStorage. Full reload, sign-out, token expiry, or a 401 clears the session
and requires sign-in again. A 403 indicates insufficient permission and keeps
an otherwise valid session. Sign-out clears this browser session; a copied token
is valid until its 30-minute expiry. Refresh tokens/revocation are not implemented.

The create form records the authenticated user on the server. The old
`VITE_DEMO_USER_ID` setting and creator ID field are no longer used, including
in production builds. API authorization is authoritative; hiding a page does
not grant or deny API access. Production hosting must use HTTPS.

`npm test` uses Node's built-in test runner and TypeScript support (Node 22.18+
or a compatible newer release), with no extra test dependencies. Tests cover
ticket response handling, empty data, HTTP/network failures, malformed data,
cancellation, create requests, lookup data, form validation, login responses,
bearer headers, expiry, logout, detail/history response validation, missing
tickets, invalid route IDs, and UTC timestamp handling.
They do not replace browser interaction checks.

The ASP.NET Core solution stays at the repository root. Browser code must never
contain database credentials,
JWT signing keys, or other server secrets.
