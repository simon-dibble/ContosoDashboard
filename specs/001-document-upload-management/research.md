# Research: Document Upload and Management

## Decision: Private opaque local storage behind `IFileStorage`

**Rationale:** Store bytes beneath a configured application-data root, outside `wwwroot`, using
cryptographically random/opaque generated names and no user-controlled path segments. The
service returns a stream only after authorization. A local adapter preserves offline training;
an object-storage adapter can later implement the same contract.

**Alternatives considered:** Saving bytes in SQL Server was rejected for simpler LocalDB
operation and because a stream boundary is the stated migration seam. Saving under `wwwroot`
was rejected because it permits public/static access.

## Decision: Compensating transaction for content plus metadata

**Rationale:** EF cannot atomically commit a filesystem write and a database transaction. Write
to a private temporary/generated location, scan, persist metadata in an EF transaction, then
promote/commit the content; delete the temporary/final file when any later step fails. A
replacement writes a new object first and only swaps the metadata pointer after validation and
scan success, retaining the old object until the database update succeeds.

**Alternatives considered:** A distributed transaction was rejected as unavailable for local
filesystem/SQL Server and unnecessary for this training scope. Storing only a database row was
rejected because it leaves an accessible orphan risk.

## Decision: Deterministic offline malware scanner

**Rationale:** Define `IMalwareScanner` with a result containing pass/fail and a safe reason code.
The training adapter uses deterministic, documented byte/filename test markers (including a
test-only EICAR-compatible marker or configured sentinel), never claims to be antivirus, and
fails closed on scanner errors. A production adapter can call a real scanner without changing
document behavior.

**Alternatives considered:** Extension-only checks were rejected because FR-006 requires a
malware gate. A network antivirus dependency was rejected by the offline-first constitution.

## Decision: Authorization-first queries and defense in depth

**Rationale:** Build an authorized `IQueryable<Document>` from current claims and live
ownership/project/team/share relationships before applying search, sorting, or pagination. Use
opaque integer IDs only as lookups inside that predicate; return the same not-found result for
unauthorized IDs. Recheck service authorization for downloads, previews, mutations, sharing,
task attachment, and delete.

**Alternatives considered:** UI-only hiding was rejected as IDOR-prone. A single role check was
rejected because project membership, team eligibility, ownership, and revocation are relational.

## Decision: Relational sharing and immutable audit snapshots

**Rationale:** Use explicit user/team share rows with uniqueness and revocation state, rather
than serializing recipients. Store action, actor, document identifiers/title/type, context,
timestamp, outcome, and reason in append-only activity rows. This preserves reportability after
document deletion and supports SQL aggregates.

**Alternatives considered:** Reusing notifications as permissions was rejected because
notifications are messages, not access grants. Reconstructing history from current documents was
rejected because permanent deletion removes the source row.

## Decision: EF migration transition

**Rationale:** The repository currently calls `EnsureCreated()` and has no migration directory.
Create a baseline/document migration using the installed EF tooling, verify it against a fresh
LocalDB database, and change startup to `Database.Migrate()` so future schema changes are
repeatable. Preserve seed data and document the one-time handling of existing training databases.

**Alternatives considered:** Continuing `EnsureCreated()` was rejected because it cannot evolve
schema safely. An external migration service was rejected for offline training.

## Decision: Existing notification and UI conventions

**Rationale:** Reuse `INotificationService`, `Notification`, Bootstrap 5.3, `Pages/*.razor`,
`Shared/NavMenu.razor`, and `DashboardService`. Add small document-specific service contracts
instead of introducing a new API or client framework.

**Alternatives considered:** A separate REST backend was rejected because the application is
Blazor Server and has no existing API boundary.
