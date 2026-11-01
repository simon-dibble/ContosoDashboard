# Data Model: Document Upload and Management

## Document

| Field | Type/constraints | Notes |
|---|---|---|
| `DocumentId` | `int` PK | Required by FR-023; never used as a storage path |
| `Title` | required string, max 255 | Trimmed; duplicate titles allowed |
| `Description` | nullable string, max 2000 | Optional |
| `Category` | required string, max 50 | Text values: Project Documents, Team Resources, Personal Files, Reports, Presentations, Other |
| `Tags` | normalized text or related tag rows | Trim, reject empty/excessive/duplicates; preserve user-defined values |
| `ProjectId` | nullable `int` FK | Must be visible/manageable by actor when set |
| `UploadedByUserId` | required `int` FK | Existing `User` |
| `TaskId` | nullable `int` FK/association | Task attachment must inherit/validate task project |
| `StorageName` | required opaque string, unique | Generated server-side; never original filename/path |
| `OriginalFileName` | required safe display string | Never used for filesystem resolution |
| `FileSize` | required `long`, 1..26,214,400 | 25 MiB maximum per file |
| `FileType` | required string, max 255 | Validated allowlist MIME/extension |
| `UploadedAt` | required UTC `DateTime` | Indexed with uploader/project as appropriate |
| `ScanStatus` | text state | Pending, Passed, Rejected; only Passed is accessible |
| `IsDeleted` | bool or deletion state | Prefer hard delete of document/content; audit remains |

## DocumentShare

Represents a specific user grant or a materialized eligible-team grant. Fields: integer
`DocumentShareId`, required `DocumentId`, required recipient `UserId` (materialized from a
department/project team selection), optional source team/department identifier, `SharedByUserId`,
`SharedAt`, nullable `RevokedAt`, and uniqueness over active document/recipient grants. A revoked
share is excluded from authorized queries immediately.

## DocumentTaskAssociation

Join entity with `DocumentId`, `TaskId`, `AttachedByUserId`, and `AttachedAt`; unique document/task
pair. The task's project must equal the document project (or establish the project during upload)
and the actor must be able to view the task and document.

## DocumentActivity

Append-only audit snapshot: integer `DocumentActivityId`, nullable `DocumentId`, nullable
`DocumentTitleSnapshot`, nullable `FileTypeSnapshot`, nullable `ProjectIdSnapshot`, required
`ActorUserId` (or a system actor convention), action text (Upload, Download, Delete, Share),
outcome text (Succeeded/Rejected/Failed), reason code, UTC `OccurredAt`, and optional correlation
identifier. No file bytes, secrets, or storage paths. Retain rows after hard deletion and restrict
reports/row access to administrators.

## Relationships and indexes

- `User 1—many Document` and `Project 1—many Document`, both restrictive on delete.
- `Document 1—many DocumentShare`, `Document 1—many DocumentActivity`,
  `Document 1—many DocumentTaskAssociation`; delete behavior must permit audit snapshot retention.
- Index `(UploadedByUserId, UploadedAt)`, `(ProjectId, UploadedAt)`, `(Category, UploadedAt)`,
  `(ScanStatus, UploadedAt)`, and share `(UserId, RevokedAt, DocumentId)`.
- Add unique index on `StorageName`; unique active association/share constraints as supported by
  the chosen schema.
- Search should use bounded, normalized columns and server-side filtering; no unbounded in-memory
  materialization for the 500-document target.

## Validation and state transitions

1. `Pending` → `Rejected` on invalid type/size or scanner failure; no accessible content.
2. `Pending` → `Passed` only after scanner pass and metadata/content compensation succeeds.
3. `Passed` → replacement `Pending` while old content remains the active valid document; on
   replacement failure, restore/retain old pointer and `Passed` state.
4. `Passed` → hard deleted document/content after authorized confirmation; append activity first/
   during the operation and retain the snapshot.
5. Share active → revoked; revocation immediately removes access and does not delete audit rows.

All timestamps are UTC. Input validation is repeated in service methods, not trusted from Blazor
component state.
