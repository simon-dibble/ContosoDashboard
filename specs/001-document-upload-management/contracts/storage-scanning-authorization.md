# Service Contracts

These are implementation contracts for the later task phase. They describe behavior, not method
bodies or a required transport format; the application is an internal Blazor Server application.

## `IFileStorage`

- `CreateTemporaryAsync(Stream content, CancellationToken)` → opaque temporary handle.
- `CommitAsync(temporaryHandle, opaqueStorageName, CancellationToken)` → committed handle.
- `OpenReadAsync(opaqueStorageName, CancellationToken)` → read-only stream.
- `DeleteAsync(opaqueStorageName, CancellationToken)` → idempotent cleanup result.
- `ExistsAsync` is optional for diagnostics only and must not bypass authorization.

The adapter must reject path traversal, never accept a browser path, keep its root private, and
not log bytes or secrets. Missing content is an auditable outcome, not a path disclosure.

## `IMalwareScanner`

`ScanAsync(Stream content, string safeFileName, string fileType, CancellationToken)` returns
`Passed`, `Rejected` with a non-sensitive reason code, or `Error`. The document service fails
closed for `Error`, and no document is accessible until `Passed`. The offline implementation is
deterministic and clearly labeled training-only.

## `IDocumentAuthorizationService`

Inputs include current authenticated user, operation, document/project/task identifier, and live
relationships. Operations include ReadMetadata, Search, Preview, Download, Edit, Replace, Share,
Delete, Attach, and AuditReport. It returns an allow decision without revealing whether an
unauthorized resource exists. Rules derive from owner, assigned project, team lead/team, project
manager/project, administrator, active share, and task viewer relationships.

## `IDocumentService`

Public behavior includes per-file batch upload results; authorized list/search with stable sorting
and paging; metadata/detail; preview/download stream acquisition; metadata edit; validated
replacement; permanent delete; share/revoke and Shared with Me; task attach; dashboard recent-five
and count; and administrator-only audit aggregates. Every method obtains the current user from
the server-side authentication context and performs service-level authorization.

## UI contract

`Pages/Documents.razor` provides title/category/description/project/tags and multi-file upload
with progress and per-file outcomes, sorting/filter/search, and authorized actions.
`DocumentDetails.razor` provides preview/download/edit/replace/share/delete. `SharedDocuments.razor`
lists active grants. `DocumentAudit.razor` is administrator-only. Existing dashboard, task,
notification, navigation, and project pages link to these flows without duplicating permission
logic. All errors are user-safe and do not expose storage paths or scanner internals.
