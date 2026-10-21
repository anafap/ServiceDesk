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
npm run build
npm run preview
```

The build checks TypeScript before generating production assets in `dist/`.
Preview serves that build locally. A production host must route frontend paths
such as `/tickets` to `index.html` for browser routing.

## Current scope

This foundation contains shared navigation, responsive layout, reusable page
headers/empty states, Overview/Tickets routes, and an unknown-route page. Ticket
data, forms, dashboard metrics, and authentication are not connected yet. No
sample ticket data or simulated authentication is presented.

The ASP.NET Core solution stays at the repository root. API integration is the
next separate change. Browser code must never contain database credentials,
JWT signing keys, or other server secrets.
