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
It cancels requests on navigation and times out requests after 15 seconds.
The New ticket form loads real stores/categories, validates required fields and
text limits, creates a request through `POST /api/tickets`, and refreshes the
list after success. Fields are preserved on failure, and duplicate submissions
are blocked while saving. An interrupted write is never retried automatically:
check the ticket list before resubmitting an unconfirmed save.
Overview summaries and authentication are not implemented yet.
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

## Local demo creator

Copy `.env.example` to `.env.local` and set `VITE_DEMO_USER_ID` to an existing
user ID verified in your local database. Restart Vite after changing environment
files. The form labels the demo creator explicitly; this is not authentication.
The current API still trusts the supplied creator ID. Server-derived identity
and authorization belong to the upcoming security milestone.

This value is a non-secret public ID. All `VITE_` values can be read in browser
code; never use them for passwords, connection strings, or signing keys.
The local file is ignored by Git. Creation is disabled when this setting is
missing/invalid and in production builds until authentication is implemented.

`npm test` uses Node's built-in test runner and TypeScript support (Node 22.18+
or a compatible newer release), with no extra test dependencies. Tests cover
ticket response handling, empty data, HTTP/network failures, malformed data,
cancellation, create requests, lookup data, form validation, and demo identity.
They do not replace browser interaction checks.

The ASP.NET Core solution stays at the repository root. Browser code must never contain database credentials,
JWT signing keys, or other server secrets.
