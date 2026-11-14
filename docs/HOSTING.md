# Hosting preparation and selection gate

The repository now has a combined React/API production Dockerfile. It does not create
cloud resources or deploy a site. Mrs.Ana chooses provider, region and spending limit
before provisioning. Keep the real pilot and portfolio demo in separate databases,
private photo storage, credentials and secrets. This release serves one business per
deployment; store-level authorization is not business-tenant isolation.

## Build and configure

```sh
docker build -t servicedesk-pilot:local .
```

The image runs as a non-root user on port 8080. Publish only through an HTTPS edge.
Provide ConnectionStrings__DefaultConnection, Jwt__Key/Issuer/Audience and a persistent
private Photos__Root mount. Never bake secrets or database files into the image.
Uploads default off; enabling them requires the worker/scanner described in PILOT.md.
Run the same image with `--photo-worker` as its command for a dedicated worker.

Use encrypted SQL connections with certificate validation and a production-licensed
SQL service. Developer edition in local Compose is for development/testing. Use a
least-privilege application database identity and a separate deployment identity for
migrations. API and worker need the same shared private storage in this first release;
replace the storage adapter before using ephemeral disks or independent replicas.

Configure exact trusted edge addresses through `Hosting__TrustedProxies__0` etc.
Forwarded headers are ignored when none are configured. One forwarding hop is accepted.
The edge must replace incoming forwarding headers. Production enables HSTS and HTTPS
redirection; set `ASPNETCORE_HTTPS_PORT=443` when the edge terminates TLS. Keep
`Hosting__RequireHttps=true` for hosted deployments. An HTTP-only localhost smoke test
may explicitly set it false; that is not a public hosting configuration.

`/health/live` is public process liveness. `/health/ready` returns 200 only when SQL is
reachable and the schema is current, otherwise 503 without internal diagnostics.
Use liveness for frequent probes and readiness sparingly on metered/serverless SQL.
Static assets and SPA routes are public; API operations retain authentication.
Unknown API/health paths and missing asset files return 404 instead of index.html.

## Migrate and provision explicitly

Generate/review an idempotent migration script using the repository EF tool; apply
through the deployment identity during a maintenance window. Do not start the new
version against an old schema. Back up and test on a restored populated copy first.
AddPilotCommunication is additive; downgrading it destroys the new discussion/photo/
notification records and is not a routine rollback. Retain database backups and
released photo objects with compatible retention. The API never migrates on startup.

Run the image once with `--provision-admin` and private Provisioning__AdminEmail and
Provisioning__AdminPassword (8–128 characters). This creates an initial Admin and
exits; repeating it never resets credentials. Remove provisioning secrets afterward.
For the separate synthetic portfolio database only, `--provision-demo` requires
`Hosting__DemoInstance=true` and the three DemoData account settings documented in
SETUP.md. Normal startup does not provision or reset accounts. Business databases
must not use demo provisioning. Admin creates stores and managers through the UI;
the provisioning command adds a General maintenance category when none exists (no category UI).

## Shared demo account protection

Set `Hosting__DemoInstance=true` on the isolated demo deployment to make account
management read-only. Authenticated account mutations return 403 Problem Details
with a trace ID and “Account changes are disabled in this demo.” This blocks profile
edits, password changes, and manager creation/editing/activation/reassignment/password
reset. Account and manager-directory reads retain their existing authorization.
Login and ticket workflows remain available. Frontend controls are unchanged;
attempted account changes display the API error. Explicit command-line provisioning
remains available to the operator.

The setting defaults to false. Keep it false or absent for normal business account
management. No schema migration is required for these restrictions.

## Provider comparison and budget constraints

Azure is the cost-first shortlist: its SQL Database free allowance renews monthly
(100,000 vCore-seconds, 32 GB data and 32 GB backup storage per eligible database).
This is an allowance, not a guarantee of continuous free availability. Compute,
storage, logs and scanning must be costed separately. Configure the cap/pause option
for a demo if avoiding charges takes priority over availability.

AWS remains a valid choice for AWS learning and deployment. New-account free-plan
credits expire; RDS SQL Server is not a permanently free database. Compare a managed
RDS Express database plus compute/storage against a self-managed Express instance,
including patching, backups and restore effort. Do not move to another database just
to obtain a free tier without separately assessing the application changes.

The photo worker polls SQL and the scanner requires memory/signature updates. This
combination has a steady running cost and can defeat scale-to-zero savings. A zero-
cost demo can leave uploads disabled; existing screenshots demonstrate photos. An
always-available business pilot requires an agreed operating budget. Budget alerts
notify; they are not a universal hard spending cap.

Before selecting a provider, produce a calculator estimate for the chosen region and
record: API/worker/scanner resources and hours, SQL engine/edition/storage/backup,
private object storage and requests, bandwidth, logs/alerts, DNS/HTTPS and any public
IP/network charges. Show first-month credits separately from the ordinary monthly
cost, and multiply independent demo/pilot resources appropriately. No region/account
eligibility or currency conversion has been assumed as a binding quote here.

Official references checked November 12, 2026:
- [Azure SQL free limits](https://learn.microsoft.com/en-us/azure/azure-sql/database/free-tier-faq?view=azuresql-db)
- [Azure Container Apps pricing](https://azure.microsoft.com/en-us/pricing/details/container-apps/)
- [AWS Free Tier terms](https://aws.amazon.com/free/free-tier-faqs/)
- [RDS SQL Server pricing](https://aws.amazon.com/rds/sqlserver/pricing/)
- [AWS Pricing Calculator](https://calculator.aws/)
- [Azure Pricing Calculator](https://azure.microsoft.com/en-us/pricing/calculator/)

## Pilot release gate

Before inviting real users: choose data region/retention with the business, name an
operator, agree acceptable data loss and recovery time, enable daily database/photo
backups, and restore both into a separate environment. Verify counts, users, history,
comments and at least one image. Configure alerts for readiness failures, API errors,
photo queue age/rejections, stale scanner signatures, backup failure and spending.
Repeat the authenticated two-role walkthrough on the hosted HTTPS URL and test
upgrade/rollback using the retained image and compatible backup. Record evidence;
these hosted operational checks have not been performed by adding a Dockerfile.
