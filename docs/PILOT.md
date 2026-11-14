# Pilot features and verification

## Ticket discussion

Admin and the ticket store's managers can post plain-text comments (1–2,000 trimmed
characters). Discussion initially shows the latest five comments, oldest to newest,
with each author and timestamp. New comments appear at the bottom. Load older
comments from above the thread, five more at a time, or refresh explicitly.
The API fetches up to 25 comments per page; older loaded comments stay hidden until
requested. No edit/delete endpoints exist. Closed and
rejected tickets keep existing comments visible but reject additions. Comments do
not create status-history events. A comment updates the ticket rowversion, so an
older edit/workflow form must refresh before saving. Failed submissions retain text;
uncertain writes are never replayed automatically.

## Sign-in destination

Every successful sign-in opens Overview, including sign-in after signing out,
refreshing a protected page, or an expired session. The previous page, filters,
and URL fragment are cleared from the sign-in destination.

## Queries and notifications

`GET /api/tickets?page=1&pageSize=25&title=printer&status=1&priority=3` returns
`{ items, totalCount, page, pageSize }`. Maximum page size is 100. Search is trimmed,
case-insensitive title substring matching; supplied filters combine with AND.
Store scope is applied in SQL before filtering, counting or paging. Ordering is
CreatedAt DESC, Id DESC. `GET /api/dashboard` returns `{ summary, recent }`, where
summary includes total/open/inProgress/resolved and recent contains at most five.
Dashboard counts cover the authorized dataset, independently of list filters.
This replaces the previous array-only ticket-list contract; update external clients.

`GET /api/notifications?beforeId=...` returns items, unreadCount and nextBeforeId.
`POST /api/notifications/{id}/read` is recipient-scoped and idempotent. Notifications
are stored atomically with new tickets, comments, workflow events/corrections and
released photos. Recipients are active Admins/current-store managers except the actor.
Current access is rechecked when reading; reassigned managers cannot see old-store
notifications. The inbox refreshes on navigation/focus and each visible minute.
Notifications contain event labels and ticket IDs, not discussion or image previews.

## Account password change

Account → Change password requires the current password, a different new password
of 8–128 characters, and frontend confirmation. `POST /api/account/change-password`
accepts `{ currentPassword, newPassword }`; actor identity is server-derived. A
tracked rowversion protects competing account updates. Success returns 204 and
invalidates prior sessions, including the current one. Sign in with the new password.
Admin reset of StoreManager passwords remains available. Email recovery is deferred.

## Photo processing

Uploads are disabled by default. Enable `Photos__Enabled=true` only after configuring
private storage, a running worker and a scanner with current signatures. The UI
shows when uploads are unavailable. Existing released photos remain readable.

Local setup (requires Docker memory for SQL and a roughly 2-GiB scanner):

```sh
docker compose -f compose.photos.yaml up -d
```

Wait for scanner readiness and initial signature updates. Set an absolute private
`Photos__Root` path in both API and worker configuration, then run a separate process:

```sh
dotnet run --project ServiceDesk.Api --launch-profile http -- --photo-worker
```

The worker runs without hosting HTTP. It uses the same database/storage configuration
as the API. `Photos__ScannerHost` defaults to 127.0.0.1 and `Photos__ScannerPort` to
3310. Keep the scanner on a trusted private network; the ClamAV TCP protocol has no
application authentication. Its timezone must be UTC. Signatures older than 48 hours
are rejected. Update signatures continuously; do not bypass failure checks.

`POST /api/tickets/{id}/attachments` accepts one multipart file, at most 5 MiB,
JPEG/PNG signatures only, with a five-active-photo quota. The limit is enforced
transactionally with a ticket rowversion check. Closed/rejected tickets reject new
photos. Accepted uploads return 202; this means queued, not approved for viewing.
`GET /api/tickets/{id}/attachments` returns `{items,uploadsEnabled}` with processing
states; only ready items can be retrieved at `/{attachmentId}/content`. Every read
requires bearer authentication and current ticket access; no public file links exist.

Originals are quarantined under generated keys and never served. A SQL-leased worker
scans the original, runs bounded image processing in a separate child process, strips
metadata, writes PNG, scans the output, then publishes metadata/notifications in one
transaction. Only JPEG/PNG coders are enabled. Image limits are 20 megapixels, 256 MiB
pixel-cache memory and a 30-second process deadline. Processing children receive no
application secrets. Deploy workers with OS/container CPU and memory limits as well.
The image processor is still handling untrusted input; keep it patched.

Scanner errors, stale signatures or timeouts keep files inaccessible. Jobs have a
three-minute lease and at most three attempts; known malware and invalid images are
rejected immediately. Originals are removed after completion/rejection. Abandoned
originals and unreferenced outputs are cleaned after 24 hours. Do not expose the
storage directory through a web server. Private local storage is implemented; a
provider-specific object-storage adapter remains a hosting-stage task.

## Checks

```sh
dotnet test
npm --prefix frontend test
npm --prefix frontend run lint
npm --prefix frontend run build
```

Configure `SERVICEDESK_TEST_SQL_CONNECTION` privately to enable isolated SQL tests.
For full browser/photo integration, run the scanner and set
`SERVICEDESK_E2E_PHOTOS=1` before `python3 scripts/run-browser-tests.py`. Override
`Photos__ScannerPort` if needed. The runner provisions a unique SQL database, private
photo directory, worker, API and frontend. Without that flag, the real scanner/photo
browser case explicitly skips. Screenshots go into the run's temporary directory;
existing portfolio screenshots are not overwritten. Test service logs stay private.

Never run migration/seed/browser commands against an uncertain database. The browser
runner overrides the configured database name before provisioning. Existing populated
installations require backup, restored-copy verification and reviewed upgrade SQL.

## Learning notes and review boundaries

Significant implementation groups:

- Domain entities and DbContext mappings retain comment evidence and attach private
  image jobs/recipient notifications to existing ticket/user relationships.
- DiscussionService/AttachmentService coordinate guarded writes; TicketService now
  publishes notifications inside its workflow transaction. Repositories keep SQL
  access and pagination out of the API and Domain.
- AccountService verifies the existing password, hashes the replacement and saves a
  tracked account; concurrent saves and old sessions fail through existing mechanisms.
- TicketDiscussion/TicketPhotos/NotificationInbox and AccountPage expose these features
  with typed API clients, cancellation, retained form input and explicit error states.
- PhotoWorker, LocalPhotoStorage, ClamAvScanner and PhotoSanitizer separate quarantine,
  signature verification, restricted decoding and transactional publication.
- Dockerfile/Program.cs add same-origin serving, health checks, explicit provisioning
  and trusted forwarding; HOSTING.md distinguishes packaging from hosted operations.

Concepts: authorization checks identity and current resource scope; a transaction
keeps several records atomic; rowversion detects an intervening update; a processing
lease permits recovery after a worker crash; an accepted upload is not a released
image. The database and filesystem do not share a transaction, so private object
creation precedes publication and abandoned objects need cleanup.

Interview questions:

1. Why update a ticket when adding a comment? The rowversion write makes a competing
   closure fail one transaction instead of allowing a comment based on stale status.
2. Why scan and re-encode? Scanning checks known malware; restricted decoding verifies
   supported image content and strips metadata. Neither replaces access control or
   guarantees safety against every vulnerability.
3. Why recheck notification scope? A recipient may have moved stores after an event;
   historical recipient identity must not preserve access to another store's data.

Independent exercise: use two manager accounts from different stores. Post a comment
on one store's ticket, check Admin's notification, and confirm the other manager
cannot retrieve either discussion or its photos. Then close the ticket as Admin and
verify that reading remains possible while new comments/uploads are blocked.

Suggested local commit boundaries (review exact diffs first; none auto-created):

1. Add scoped append-only ticket discussion.
2. Move ticket filtering and dashboard summaries to SQL queries.
3. Add current-password account changes and session invalidation.
4. Publish transactional in-app ticket notifications.
5. Quarantine and verify private ticket photos with a leased worker.
6. Package the pilot application and document deployment gates.

The communication migration groups three related new tables. Keep its generated
metadata/snapshot together and coordinate schema rollout with the associated code.

## Verification record — November 12, 2026

140 backend tests passed with real SQL enabled and zero skipped; 70 frontend tests,
Oxlint and TypeScript/Vite builds passed. Four Chromium walkthroughs passed, including
real ClamAV quarantine/processing/private access, comment rendering, password changes,
notifications and mobile layout. Generated screenshots were visually inspected.

Production image built and passed local smoke checks for SPA deep links, missing
assets/API paths, authentication/store scope, database readiness and non-root execution.
The Linux sanitizer passed with no networking and a read-only/resource-limited runtime.
A harmless EICAR fixture was detected by the real scanner. Disposable populated-schema
migration checks preserved accounts, hashes and tickets. This does not establish a
restored-copy upgrade or hosted recovery of an existing business database.

The original database was not migrated. Hosting, region/cost selection, production
backup/restore and monitoring remain deployment-stage work. No commits or pushes
were made. Tests used separate temporary containers/databases and generated credentials.

Visit coordination now requires store confirmation and a contact before repair
starts. See [visit workflow and contracts](VISIT_CONFIRMATION.md).
