# Implementation Plan: Document Upload and Management

**Branch**: `main` | **Date**: 2026-11-02 | **Spec**: [spec.md](spec.md)
**Input**: `specs/001-document-upload-management/spec.md`

## Summary

Add an offline-first document repository to the existing .NET 9 Blazor Server application.
The feature will persist integer-keyed document metadata in `ApplicationDbContext`, keep file
bytes behind an injected storage abstraction outside `wwwroot`, gate availability on a
deterministic offline malware-scanner contract, and centralize resource authorization before
every query or file operation. Blazor pages will expose upload, browse/search/filter, preview
/download, edit/replace, share, task attachment, dashboard, and administrator audit flows while
reusing the existing users, projects, tasks, notifications, and role model.

Implementation is intentionally not included in this planning phase; detailed work belongs in
the later `/speckit.tasks` and implementation phases.

## Technical Context

**Language/Version**: C# on .NET 9 (`net9.0`); nullable and implicit usings enabled  
**Primary Dependencies**: ASP.NET Core Blazor Server, EF Core SQL Server provider (currently
8.x in `ContosoDashboard.csproj`), Bootstrap 5.3, existing cookie mock authentication  
**Storage**: SQL Server LocalDB for metadata; private local filesystem adapter for content;
replaceable production adapter boundary  
**Testing**: Existing repository test conventions are not present in the source tree; add a
focused test project/fixtures in the implementation phase using the repository-supported .NET
test framework, plus EF/database and browser/manual validation documented in `quickstart.md`  
**Target Platform**: Windows local training environment, ASP.NET Core 9 Blazor Server, offline  
**Project Type**: Server-rendered web application, single project  
**Performance Goals**: 95% of valid uploads <=25 MB in 30 seconds; list/search for up to 500
authorized records in 2 seconds; PDF/image preview in 3 seconds (SC-005–SC-008)  
**Constraints**: No required network/cloud dependency; content never under `wwwroot`; maximum
25 MB per file; supported MIME/extension allowlist; every resource operation must authorize;
audit metadata survives document deletion  
**Scale/Scope**: Training-scale LocalDB deployment, at least 500 accessible documents, multiple
users/projects/teams, single-file and multi-file upload with per-file outcomes

## Constitution Check

*GATE: evaluated before Phase 0 and again after Phase 1.*

| Principle/constraint | Design response | Status |
|---|---|---|
| I. Offline-first | LocalDB, private local storage, deterministic scanner, in-app notifications; cloud adapters are optional future replacements only. | PASS |
| II. Secure-by-default | `[Authorize]` pages plus service-level policy checks; query authorization before filtering/search; opaque storage names; no client-supplied path; audit access administrator-only; generic not-found/denied responses. | PASS |
| III. Layered/replaceable | `Models`, `Data`, `Services`, and `Pages` remain boundaries; `IFileStorage`, `IMalwareScanner`, and document authorization interfaces are DI contracts. | PASS |
| IV. Testable/reviewable | Unit, authorization/IDOR, scanner, storage failure, EF integration, UI, and performance/security scenarios are explicit in the plan and quickstart. | PASS |
| V. Simplicity/traceability | Reuses existing `User`, `Project`, `ProjectMember`, `TaskItem`, `Notification`, and role conventions; no versioning, quotas, or external analytics. | PASS |
| Technology/security constraints | net9.0 Blazor Server and SQL Server LocalDB remain the target; EF migrations replace `EnsureCreated` for the feature database transition; secure cookie/header configuration is preserved. | PASS |

No constitution violations or exceptions require complexity tracking.

## Project Structure

```text
ContosoDashboard/
├── Models/
│   ├── Document.cs
│   ├── DocumentShare.cs
│   ├── DocumentActivity.cs
│   └── DocumentTaskAssociation.cs
├── Data/
│   ├── ApplicationDbContext.cs
│   └── Migrations/                 # generated EF migration(s)
├── Services/
│   ├── DocumentService.cs
│   ├── DocumentAuthorizationService.cs
│   ├── FileStorageService.cs       # IFileStorage + private local adapter
│   ├── MalwareScanner.cs           # contract + deterministic offline adapter
│   └── DocumentAuditService.cs
├── Pages/
│   ├── Documents.razor
│   ├── DocumentDetails.razor
│   ├── SharedDocuments.razor
│   └── DocumentAudit.razor
├── Shared/                         # nav/dashboard/task integration
└── Program.cs                      # DI and options registration
```

Tests should be placed in a separate `ContosoDashboard.Tests/` project (unit, integration, and
security/UI fixtures) rather than coupling production services to a test framework.

**Structure Decision**: Extend the existing single ASP.NET Core project and its established
Models/Data/Services/Pages layers. Add only the feature-specific services and models above;
reuse `NotificationService`, dashboard/task pages, and existing authorization state. A private
storage root is configured outside `wwwroot`; it must not be accepted from request data.

## Design Phases

### Phase 0 — Research

Research decisions are recorded in [research.md](research.md), including transactional
file/metadata compensation, deterministic scanning, authorization ordering, audit retention,
and EF migration compatibility with the current `EnsureCreated()` startup behavior.

### Phase 1 — Design and contracts

- [data-model.md](data-model.md) defines entities, relationships, indexes, validation, lifecycle,
  and deletion/audit rules.
- [contracts/](contracts/) defines the storage, scanner, authorization, service, and UI/API
  boundaries without prescribing implementation bodies.
- [quickstart.md](quickstart.md) defines migration, offline run, acceptance, security, failure,
  and performance validation.

### Later implementation/task phase

Generate `tasks.md` with `/speckit.tasks`, then implement in dependency order: models and
migration; storage/scanner contracts and adapters; authorization/service transactions; audit and
notifications; pages and dashboard/task integration; tests; migration from `EnsureCreated`; and
performance/security validation. Do not create `tasks.md` as part of this plan.

## Cross-Cutting Implementation Decisions

1. **Upload pipeline**: validate size, extension/MIME allowlist, title/category/tags and context;
   authorize project/task; scan a private temporary stream; persist under a generated opaque name;
   save metadata and associations; compensate storage on database failure. A failed replacement
   leaves the prior valid version untouched.
2. **Availability state**: metadata is not discoverable, previewable, downloadable, or
   shareable until scanning and persistence succeed. Per-file results are returned for batches.
3. **Authorization/IDOR**: resolve the current authenticated user server-side; build authorized
   query predicates from ownership, project membership/management, team/department shares, and
   administrator role before applying search or exposing identifiers. Recheck authorization in
   the service for every mutation and file stream.
4. **Sharing/notifications**: materialize selected users or eligible team recipients as
   deduplicated `DocumentShare` rows after validating the actor's right to share. Create in-app
   notifications through `INotificationService`; project additions notify current project
   members, respecting existing notification preferences.
5. **Audit**: append immutable activity rows for upload, download, delete, share, and outcomes;
   snapshot document ID/title/type/uploader/context so deletion cannot erase reportable details.
   Administrator reports aggregate SQL-queryable rows and never include bytes.
6. **EF migration path**: add a named migration for document tables/indexes/relationships; update
   startup to apply migrations in the local training environment only after preserving seed
   behavior. Do not mix `EnsureCreated` and migrations for the same database. Document backup/
   rollback and production adapter steps.

## Complexity Tracking

No violations. The storage and scanner abstractions are required by FR-006/FR-022 and the
constitution's replaceability rule, not speculative infrastructure.

## Post-Design Constitution Check

**Result: PASS.** The design remains offline-first, keeps authorization at both UI and service
boundaries, isolates private content, uses replaceable interfaces, and provides focused
verification. The only operational prerequisite is completing the EF migration transition before
deployment; it is documented in [quickstart.md](quickstart.md) and is not an exception.
