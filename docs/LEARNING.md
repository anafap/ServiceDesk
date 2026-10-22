# Understand and explain this increment

## What changed and where

| Change | Significant code | Why it exists |
|---|---|---|
| Manager repair verification | `ServiceDeskEndpoints`, frontend `ticketWorkflow` rules, security tests | Both UI and API allow own-store progress/resolve/close; the server remains authoritative. |
| Administration | `AdministrationService`, `AdministrationRepository`, `AdministrationPage` | Separates validated business operations, SQL persistence, and interactive forms. |
| Readable references | `DisplayNameRepository`, ticket/session response DTOs, frontend display helpers | Resolves only authorized reference IDs, retaining history for inactive records. |
| Demo provisioning | `DemoDataSeeder`, SQL seed tests | Creates reproducible two-store data and hashed accounts without resetting passwords or duplicating edited tickets. |
| Repeatable setup | `compose.yaml`, local EF tool manifest, setup/demo guides | Lets a reviewer run the application without developer-specific configuration. |

## Concepts to understand

- A JWT proves identity and carries role/store claims; each API operation still
  checks what that identity may access. Hiding a React button is only usability.
- A database transaction groups writes. It does not solve every simultaneous
  update conflict; optimistic concurrency is a separate concern.
- Deactivation is a business state. It removes a record from new-work choices
  while preserving existing references and the ability to finish repairs.
- DTOs control the public contract. Adding display names is safer than sending
  full User entities or fetching an unrestricted directory in the browser.
- Batched ID lookups avoid a separate database request for every displayed row.
- React forms use controlled values and derive validation; failed submissions
  retain input. Reads can be cancelled; uncertain writes are not silently retried.
- Idempotent setup means re-running it preserves the intended state. A generated
  password is hashed when creating an account, not reapplied on every startup.
- Clean-checkout verification exposes missing dependencies/configuration hidden
  by local caches. Here it caught a vulnerable transitive cryptography package.

## Three interview questions

1. **How do you stop a manager changing a ticket in another store?**
   The API reads the ticket, compares its store to the validated identity's store,
   and returns 404 for inaccessible or missing tickets before invoking the use case.
2. **Why deactivate technicians instead of deleting them?**
   Existing assignments must remain understandable. New assignments require an
   active technician, while historical reads include inactive records.
3. **How do you know the setup and workflow are reproducible?**
   A clean clone restored dependencies, started a separate SQL container, migrated
   an empty database, seeded twice, and passed the real three-account HTTP
   walkthrough plus SQL transaction/administration/seed tests.

## Independent exercise

Using demo data, assign a technician to a ticket, deactivate that technician,
and verify two things yourself: the saved assignment still shows their name,
and they are absent from the next assignment form. Reactivate them afterward.
Explain which API checks enforce the behavior even if the browser UI is bypassed.

## Incremental commits

- `8b0c1be`: Allow store managers to verify repair progress and resolution.
- `5f8ecd1`: Add administration and named two-role ticket workspaces.
- `4917444`: Document reproducible SQL setup and two-role portfolio demo.
- `7b97156`: Patch API cryptography dependency found in clean restore.

These are local checkpoints; they have not been pushed. Screenshot capture and
new-screen browser review are pending computer-use access or manual review.
