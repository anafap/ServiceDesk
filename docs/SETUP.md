# Run ServiceDesk locally

## Prerequisites

- .NET SDK 10, Node.js 22.18+ (or a compatible newer version), npm, and Git.
- Docker with sufficient memory for SQL Server 2022. SQL Server's Linux image is
  x86-64; ARM machines need Docker's compatible emulation support. A separately
  installed SQL Server may be used instead.
- The API and frontend run natively. Compose hosts SQL only; this is a local
  portfolio setup, not a production deployment.

## Database and private configuration

From the repository root:

```sh
cp .env.example .env
```

Edit `.env` and give `SQL_PASSWORD` a unique strong password accepted by SQL
Server (at least 8 characters, using uppercase, lowercase, digits, and symbols).
Leave `SQL_PORT=14334` unless that port is occupied. `.env` is ignored by Git.
Do not commit passwords, JWT keys, connection strings, or populated environment files.

```sh
docker compose up -d sql
docker compose ps
dotnet restore
dotnet tool restore
npm --prefix frontend ci
```

Wait for SQL to become healthy. The Compose project uses its own named volume
and localhost-only port 14334; it does not reuse the existing `servicedesk-sql`
container. Stopping it with `docker compose down` preserves the database volume.
Do not remove the volume unless deliberately discarding this local database.

Configure these keys using `dotnet user-secrets --project ServiceDesk.Api set`
or the equivalent environment variables. Supply your own values locally:

| Configuration key | Environment variable | Value |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | `Server=localhost,14334;Database=ServiceDeskPortfolio;User Id=sa;Password=<your SQL password>;Encrypt=True;TrustServerCertificate=True` |
| `Jwt:Key` | `Jwt__Key` | A randomly generated secret of at least 32 UTF-8 bytes |
| `Jwt:Issuer` | `Jwt__Issuer` | `ServiceDesk.Local` |
| `Jwt:Audience` | `Jwt__Audience` | `ServiceDesk.Frontend` |
| `DemoData:Admin:Email` | `DemoData__Admin__Email` | Your demo Admin email, e.g. `admin@example.test` |
| `DemoData:Admin:Password` | `DemoData__Admin__Password` | Unique local password, at least 8 characters |
| `DemoData:Manager1:Email` | `DemoData__Manager1__Email` | `manager1@example.test` |
| `DemoData:Manager1:Password` | `DemoData__Manager1__Password` | Unique local password, at least 8 characters |
| `DemoData:Manager2:Email` | `DemoData__Manager2__Email` | `manager2@example.test` |
| `DemoData:Manager2:Password` | `DemoData__Manager2__Password` | Unique local password, at least 8 characters |

For example, `dotnet user-secrets --project ServiceDesk.Api set Jwt:Issuer
ServiceDesk.Local` sets one non-secret value. For passwords, avoid literal values
in shared terminal history; use a private environment/secret-management workflow.
Environment values override user-secrets. Existing clones share this project's
user-secrets ID: choose the connection deliberately before migrating or seeding.
`TrustServerCertificate=True` is for the local development SQL instance only.

## Migrate, provision demo data, and start

```sh
dotnet ef database update --project ServiceDesk.Infrastructure --startup-project ServiceDesk.Api
dotnet run --project ServiceDesk.Api --launch-profile http -- --seed-demo
```

Demo provisioning runs once and exits. It is allowed only in Development and
requires all three accounts to be configured before database writes begin. It
creates `DEMO-01` / `DEMO-02`, one manager per store, a demo technician/category,
and one starting ticket for each manager. Accounts use hashed passwords.
Re-running it does not reset passwords or overwrite edits. Reserved store-code
or account-role/store conflicts stop provisioning and roll it back. Account
creation/reassignment screens are not part of this release.

Normal startup does not run migrations or the full demo seed. The earlier
`DemoUser:Email` / `DemoUser:Password` startup configuration is supported for
existing development setups; omit those legacy keys for a fresh setup.

Terminal 1:

```sh
dotnet run --project ServiceDesk.Api --launch-profile http
```

Terminal 2:

```sh
npm --prefix frontend run dev -- --host 127.0.0.1
```

Open http://127.0.0.1:5173 and sign in using your configured account credentials.
Swagger is at http://localhost:5200/swagger. Vite proxies `/api` to port 5200.
Full browser reload requires signing in again because tokens stay in memory.

## Verification

```sh
dotnet build
dotnet test
npm --prefix frontend test
npm --prefix frontend run lint
npm --prefix frontend run build
```

Database-free backend tests explicitly skip SQL cases. To include real SQL
transaction, administration, and seed tests, privately set
`SERVICEDESK_TEST_SQL_CONNECTION` to your local SQL connection and run
`dotnet test`. The login must be allowed to create/drop databases. Tests replace
the database name with a unique test name, apply migrations, and remove it after
each case; they never migrate or clear the supplied database. See the root README.

## Troubleshooting

- SQL startup fails: inspect Docker logs locally, check memory/emulation support,
  and ensure the password meets SQL Server requirements.
- Port conflict: select another `SQL_PORT` and update the connection string.
- API configuration error: check connection/JWT settings and Development mode.
- Demo seed cannot sign in: seed deliberately preserves existing passwords;
  changing seed configuration does not reset an existing account.
- Database missing: apply migrations before demo provisioning or API startup.
- Frontend API errors: start the API on 5200; use the Vite dev server for proxy
  integration. Production preview does not proxy the API.
