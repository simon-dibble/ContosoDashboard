---
description: "Dependency-ordered implementation tasks for document upload and management"
---

# Tasks: Document Upload and Management

**Input**: Design documents from `specs/001-document-upload-management/`  
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `quickstart.md`, and `contracts/storage-scanning-authorization.md`  
**Technology target**: ASP.NET Core 9.0 Blazor Server, EF Core SQL Server/LocalDB, Bootstrap 5.3, offline-first

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish the production and test project structure without changing application behavior.

- [X] T001 Create the `ContosoDashboard.Tests/ContosoDashboard.Tests.csproj` .NET 9 xUnit test project, reference `ContosoDashboard/ContosoDashboard.csproj`, and add `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, `coverlet.collector`, and `bunit` packages
- [X] T002 [P] Create test fixture directories `ContosoDashboard.Tests/Models`, `ContosoDashboard.Tests/Services`, `ContosoDashboard.Tests/Data`, `ContosoDashboard.Tests/Components`, and `ContosoDashboard.Tests/Performance`
- [X] T003 [P] Add test configuration and isolated private storage/database settings in `ContosoDashboard.Tests/appsettings.json`, with no cloud credentials or committed uploaded files
- [X] T004 [P] Add feature configuration keys for the private storage root and upload limits in `ContosoDashboard/appsettings.json` and `ContosoDashboard/appsettings.Development.json`, keeping the root outside `wwwroot`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build shared persistence, contracts, authorization, and error boundaries before any user story can be implemented.

**âš ï¸ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 Add entities in `ContosoDashboard/Models/Document.cs`, `ContosoDashboard/Models/DocumentShare.cs`, `ContosoDashboard/Models/DocumentTaskAssociation.cs`, and `ContosoDashboard/Models/DocumentActivity.cs` with integer identifiers and the data-model constraints: `Title` required/max 255, `Description` nullable/max 2000, `Category` required/max 50 with the six specified values, `Tags` trimmed with empty/excessive/duplicate values rejected, `ProjectId` nullable, `UploadedByUserId` required, `TaskId` nullable, `StorageName` required/opaque/unique, `OriginalFileName` required/safe-display-only, `FileSize` required/1..26,214,400, `FileType` required/max 255, required UTC timestamps, and Pending/Passed/Rejected scan states
- [X] T006 Configure entity relationships, restrictive user/project foreign keys, audit-preserving delete behavior, indexes `(UploadedByUserId, UploadedAt)`, `(ProjectId, UploadedAt)`, `(Category, UploadedAt)`, `(ScanStatus, UploadedAt)`, `(UserId, RevokedAt, DocumentId)`, unique `StorageName`, and unique document/task and active document/share constraints in `ContosoDashboard/Data/ApplicationDbContext.cs`
- [X] T007 [P] Define `IFileStorage` in `ContosoDashboard/Services/FileStorageService.cs`, `IMalwareScanner` in `ContosoDashboard/Services/MalwareScanner.cs`, `IDocumentAuthorizationService` in `ContosoDashboard/Services/DocumentAuthorizationService.cs`, and `IDocumentService` in `ContosoDashboard/Services/DocumentService.cs` matching `specs/001-document-upload-management/contracts/storage-scanning-authorization.md`, including cancellation, safe streams, per-file results, and non-disclosing authorization outcomes
- [X] T008 [P] Implement shared document validation for the six allowed category values, PDF/Word/Excel/PowerPoint/plain-text/JPEG/PNG MIME-extension allowlist, 25 MiB boundary, trimmed title/description, normalized non-empty non-duplicate tags, safe filenames, and valid date ranges in `ContosoDashboard/Services/DocumentValidationService.cs`
- [X] T009 Implement private local filesystem storage in `ContosoDashboard/Services/FileStorageService.cs` with generated opaque names, path traversal/browser-path rejection, temporary-create/commit/open/delete operations, idempotent cleanup, private-root checks, and logs containing no bytes, secrets, or paths
- [X] T010 Implement deterministic training-only malware scanning in `ContosoDashboard/Services/MalwareScanner.cs`, returning Passed/Rejected/Error with non-sensitive reason codes, recognizing the documented sentinel, and failing closed on scanner errors
- [X] T011 Implement authorization-first predicates and operation checks in `ContosoDashboard/Services/DocumentAuthorizationService.cs` for owner, assigned project, team lead/team, project manager, administrator, active shares, and task viewers; return the same not-found/denied result for unauthorized identifiers and re-evaluate live membership/revocation
- [X] T012 Register storage options, `IFileStorage`, `IMalwareScanner`, authorization, validation, and document service dependencies in `ContosoDashboard/Program.cs` without adding network/cloud requirements
- [X] T013 Create the named EF migration `AddDocumentManagement` under `ContosoDashboard/Data/Migrations/` using the installed .NET 9 EF tooling and verify it creates document tables, relationships, indexes, and constraints against a fresh LocalDB database
- [X] T014 Replace `EnsureCreated()` with `Database.Migrate()` in `ContosoDashboard/Program.cs`, preserve seed users/projects/members/tasks/notifications, and document backup, one-time existing training database handling, rollback, and production adapter guidance in `README.md`

**Checkpoint**: Persistence, replaceable storage/scanner seams, validation, authorization, DI, and repeatable migrations are ready; user stories may now proceed independently.

---

## Phase 3: User Story 1 - Upload and Organize a Document (Priority: P1) ðŸŽ¯ MVP

**Goal**: Authenticated employees can upload supported files securely, receive per-file progress/outcomes, and find complete metadata in their authorized document list.

**Independent Test**: Upload a supported file <=25 MiB with title/category and optional metadata as an authenticated employee; verify it is stored privately, passes the scanner, appears in My Documents, and is visible to an authorized project member. Repeat with invalid type/size/title/category/tags and the scanner sentinel to verify rejection and no discoverable content.

### Tests for User Story 1

- [X] T015 [P] [US1] Add model and validation tests in `ContosoDashboard.Tests/Models/DocumentValidationTests.cs` for required/max-length fields, all allowed categories/types, the exact 25 MiB boundary, tag normalization/rejection, UTC timestamps, invalid dates, and integer/text conventions
- [X] T016 [P] [US1] Add storage contract tests in `ContosoDashboard.Tests/Services/FileStorageServiceTests.cs` for opaque names, path traversal/browser-path rejection, private-root enforcement, read-only streams, idempotent deletion, and cleanup
- [X] T017 [P] [US1] Add scanner tests in `ContosoDashboard.Tests/Services/MalwareScannerTests.cs` for deterministic pass, sentinel rejection, scanner error, safe reason codes, and fail-closed availability
- [X] T018 [P] [US1] Add upload transaction tests in `ContosoDashboard.Tests/Services/DocumentUploadTests.cs` for multi-file per-file outcomes and compensation after validation, scan, storage, metadata, and commit failures, proving no accessible orphan or unusable record
- [X] T019 [P] [US1] Add authorization tests in `ContosoDashboard.Tests/Services/DocumentAuthorizationTests.cs` for employee/project-member visibility, project context checks, generic unauthorized lookup responses, and no leakage of metadata/storage names
- [X] T020 [P] [US1] Add EF integration tests in `ContosoDashboard.Tests/Data/DocumentSchemaTests.cs` for model constraints, indexes, scan-state transitions, and persistence of authorized project associations

### Implementation for User Story 1

- [X] T021 [US1] Implement authorized upload/list/detail workflows, per-file validation and progress results, scan-gated Pendingâ†’Passed/Rejected transitions, compensating storage/metadata transaction, and project visibility in `ContosoDashboard/Services/DocumentService.cs`
- [X] T022 [US1] Implement My Documents/project document UI in `ContosoDashboard/Pages/Documents.razor` with title/category/description/project/tags, multi-file progress, per-file success/error feedback, stable sortable metadata list, empty/no-match/500-record states, and no client-controlled authorization
- [X] T023 [US1] Add authenticated navigation entry and route protection for documents in `ContosoDashboard/Shared/NavMenu.razor` and `ContosoDashboard/Pages/Documents.razor`, preserving mock-auth training labeling and safe user-facing errors
- [X] T024 [US1] Add the US1 end-to-end/component fixture in `ContosoDashboard.Tests/Components/DocumentsUploadTests.cs` covering supported upload, project visibility, rejected files, scanner rejection, progress, three-action normal completion, and list/detail metadata

**Checkpoint**: User Story 1 is independently usable and demonstrably secure; stop here for the MVP validation/demo.

---

## Phase 4: User Story 2 - Find, View, and Use Authorized Documents (Priority: P1)

**Goal**: Authorized users can browse, sort, filter, search, preview, and download documents while authorization is applied before query shaping and file access.

**Independent Test**: Seed owned, project, shared, revoked, and inaccessible documents; verify permitted users can list/search/filter/sort and preview/download supported files, while unauthorized users receive the same not-found/denied result and cannot infer existence.

### Tests for User Story 2

- [X] T025 [P] [US2] Add authorized query tests in `ContosoDashboard.Tests/Services/DocumentSearchAuthorizationTests.cs` for authorization-first query ordering, title/description/tags/uploader/project search, category/project/date filters, stable sorting/paging, revocation, and no IDOR leakage
- [X] T026 [P] [US2] Add stream access tests in `ContosoDashboard.Tests/Services/DocumentAccessTests.cs` for authorized download, PDF/JPEG/PNG preview, unsupported-preview download fallback, rejected/pending/missing content, and safe audit outcomes
- [X] T027 [P] [US2] Add list/search performance tests in `ContosoDashboard.Tests/Performance/DocumentQueryPerformanceTests.cs` using 500 authorized plus inaccessible records, asserting p95 list/search under 2 seconds without unbounded in-memory materialization

### Implementation for User Story 2

- [X] T028 [US2] Extend `ContosoDashboard/Services/DocumentService.cs` with authorization-first bounded IQueryable list/search/filter/sort/page operations and secure preview/download stream acquisition for only Passed documents
- [X] T029 [US2] Implement authorized browse/search/filter/sort/preview/download controls in `ContosoDashboard/Pages/Documents.razor` and `ContosoDashboard/Pages/DocumentDetails.razor`, including date validation, PDF/image inline preview, and download fallback for unsupported preview types
- [X] T030 [US2] Add the independent US2 UI/security fixture in `ContosoDashboard.Tests/Components/DocumentBrowseAccessTests.cs` for empty/no-match/500 states, all controls, preview/download, guessed IDs/URLs, revoked shares, and post-membership-change access

---

## Phase 5: User Story 3 - Maintain and Share Documents (Priority: P2)

**Goal**: Owners and authorized project managers can edit, replace, share, revoke, and permanently delete documents with notifications and immutable audit snapshots.

**Independent Test**: As an owner, edit metadata, replace successfully, share with a user and eligible department/project team, verify notifications and Shared with Me, revoke access, then delete after confirmation; separately verify project-manager access and non-member denial.

### Tests for User Story 3

- [X] T031 [P] [US3] Add edit/replacement tests in `ContosoDashboard.Tests/Services/DocumentMaintenanceTests.cs` for metadata validation, successful replacement, validation/scanner/storage failure retaining the previous Passed content, and owner/project-manager/non-member authorization
- [ ] T032 [P] [US3] Add sharing tests in `ContosoDashboard.Tests/Services/DocumentSharingTests.cs` for selected-user and eligible department/project-team expansion, deduplication, active/revoked grants, notification recipients/preferences, Shared with Me, and authorization
  - **Left unchecked (partial implementation only):** `DocumentSharingTests.cs` exists with 4 passing tests covering selected-user sharing + dedup, revoke, Shared-with-Me (active vs. revoked), and non-owner/non-manager denial. However, two parts of this task's acceptance criteria have no corresponding production implementation anywhere in the codebase, so they cannot be honestly tested: (1) automatic department/project-team membership expansion when sharing (`DocumentShareRequest`/`DocumentService.ShareAsync` only accept an explicit `UserIds` list; `SourceTeam` is stored as a label only and never drives recipient selection), and (2) per-user notification recipients/preferences (`NotificationService` has no preferences concept at all). Implementing these would require new domain features beyond test-writing scope. See the explanatory comment at the top of `DocumentSharingTests.cs`.- [X] T033 [P] [US3] Add delete/audit tests in `ContosoDashboard.Tests/Services/DocumentDeletionTests.cs` for confirmation, missing content, permanent document/content removal, retained title/type/identifier snapshot, accurate outcomes, and no path disclosure

### Implementation for User Story 3

- [X] T034 [US3] Implement metadata edit, replacement compensation, permanent delete, sharing/revocation, Shared with Me, and notification/audit orchestration in `ContosoDashboard/Services/DocumentService.cs` and `ContosoDashboard/Services/DocumentAuditService.cs`
- [X] T035 [US3] Implement edit/replace/share/revoke/delete details UI with confirmation and safe errors in `ContosoDashboard/Pages/DocumentDetails.razor`
- [X] T036 [US3] Implement active shared-document list in `ContosoDashboard/Pages/SharedDocuments.razor`, including revoked-access exclusion and notification context
- [X] T037 [US3] Add the US3 component/integration fixture in `ContosoDashboard.Tests/Components/DocumentMaintenanceTests.cs` covering owner and project-manager flows, notifications, Shared with Me, revocation, failed replacement retention, and confirmed deletion

---

## Phase 6: User Story 4 - Use Documents in Tasks and the Dashboard (Priority: P2)

**Goal**: Authorized employees can attach/upload task documents and see recent-five and count summaries with project notifications.

**Independent Test**: As an authorized task viewer, attach an existing document and upload a related one, verify task/project association and permission checks, then verify dashboard recent five/count and project-member notifications.

### Tests for User Story 4

- [X] T038 [P] [US4] Add task association tests in `ContosoDashboard.Tests/Services/DocumentTaskAssociationTests.cs` for existing attach, task upload, task/project equality, unique document/task pairs, authorized viewers, and unauthorized viewers
- [X] T039 [P] [US4] Add dashboard/notification tests in `ContosoDashboard.Tests/Services/DocumentDashboardTests.cs` for current-user recent five/count, project-member recipient filtering, preference handling, deduplication, and no cross-user leakage

### Implementation for User Story 4

- [X] T040 [US4] Implement task attachment/upload validation and authorized project inheritance in `ContosoDashboard/Services/DocumentService.cs`, persisting `DocumentTaskAssociation` with server-side task/project checks
- [X] T041 [US4] Integrate document attach/upload controls and authorized document display into `ContosoDashboard/Pages/Tasks.razor`, preserving existing task permission logic and adding no duplicate authorization source
- [X] T042 [US4] Integrate current-user recent-five uploads and document count into `ContosoDashboard/Pages/Index.razor` and `ContosoDashboard/Services/DashboardService.cs`, plus project-document notification creation through `ContosoDashboard/Services/NotificationService.cs`
- [X] T043 [US4] Add navigation links for task/document and dashboard flows in `ContosoDashboard/Shared/NavMenu.razor` and complete the US4 component/integration fixture in `ContosoDashboard.Tests/Components/DocumentTaskDashboardTests.cs`

---

## Phase 7: User Story 5 - Audit Document Activity (Priority: P3)

**Goal**: Administrators can inspect immutable document activity and SQL-backed aggregate reports; non-administrators cannot access audit data.

**Independent Test**: Perform uploads, downloads, shares, and deletions as multiple roles; verify administrator records and aggregates include actor/outcome/document snapshots after deletion, while non-administrators receive denial without leakage.

### Tests for User Story 5

- [X] T044 [P] [US5] Add audit authorization and snapshot tests in `ContosoDashboard.Tests/Services/DocumentAuditTests.cs` for append-only upload/download/share/delete outcomes, UTC timestamps, no bytes/secrets/paths, post-delete retention, and administrator-only access
- [X] T045 [P] [US5] Add aggregate report tests in `ContosoDashboard.Tests/Services/DocumentAuditReportTests.cs` for most-uploaded types, most-active uploaders, access patterns, bounded SQL queries, and non-administrator denial

### Implementation for User Story 5

- [X] T046 [US5] Implement append-only audit recording and administrator aggregate queries in `ContosoDashboard/Services/DocumentAuditService.cs`, with immutable snapshots and bounded SQL aggregation
- [X] T047 [US5] Implement administrator-only audit page and safe report states in `ContosoDashboard/Pages/DocumentAudit.razor`, applying both page and service authorization
- [X] T048 [US5] Add administrator audit navigation and the US5 component/integration fixture in `ContosoDashboard/Shared/NavMenu.razor` and `ContosoDashboard.Tests/Components/DocumentAuditTests.cs`

---

## Phase 8: Polish & Cross-Cutting Validation

**Purpose**: Validate the complete clarified scope, migration transition, security, performance, offline operation, and documentation without adding unsupported scope.

- [X] T049 [P] Add concurrency tests in `ContosoDashboard.Tests/Services/DocumentConcurrencyTests.cs` for simultaneous opaque-name generation, duplicate titles, concurrent replacement, and cleanup races
- [X] T050 [P] Add end-to-end security tests in `ContosoDashboard.Tests/Security/DocumentIdorSecurityTests.cs` for every operation (search, metadata, preview, download, edit, replace, share, delete, attach, audit) across owner, member, team lead, manager, administrator, non-member, and revoked-share roles
- [X] T051 [P] Add upload/preview performance tests in `ContosoDashboard.Tests/Performance/DocumentFilePerformanceTests.cs` asserting p95 valid <=25 MiB upload under 30 seconds and PDF/image preview under 3 seconds under typical local conditions
- [X] T052 Run the complete test/build/migration validation from `specs/001-document-upload-management/quickstart.md`, including `dotnet test`, `dotnet build`, `dotnet ef database update`, clean LocalDB seed verification, offline/no-network run, and manual acceptance flows; record unavailable tooling or environment limits in `README.md` (build clean, 98/98 tests pass, migration validated against a fresh LocalDB database without touching the existing one; offline/no-network run and manual browser acceptance flows documented as unavailable in this environment in README's new validation section)
- [X] T053 Review `ContosoDashboard/Program.cs`, `ContosoDashboard/Pages/Documents.razor`, `ContosoDashboard/Pages/DocumentDetails.razor`, `ContosoDashboard/Pages/SharedDocuments.razor`, `ContosoDashboard/Pages/DocumentAudit.razor`, and document services under `ContosoDashboard/Services/` for secure cookies/headers, `[Authorize]` boundaries, service checks, no storage-path/secret/byte disclosure, no cloud dependency, and mock-auth training labeling; record the security review in `README.md`
- [X] T054 Verify migration and compatibility behavior in `ContosoDashboard/Data/Migrations/`, `ContosoDashboard/Data/ApplicationDbContext.cs`, and `ContosoDashboard/Program.cs` by testing a fresh database and documented existing training database backup/upgrade path without mixing `EnsureCreated()` and migrations
- [X] T055 Update `README.md` with the private storage configuration, supported types/limits, deterministic scanner warning, migration/backup procedure, offline operation, production adapter seams, validation commands/results, and explicitly excluded scope

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 Setup**: No dependencies; T002â€“T004 can run in parallel after T001 establishes the test project.
- **Phase 2 Foundational**: Depends on Phase 1. T005â€“T012 establish models, contracts, adapters, authorization, and DI; T013 depends on T005â€“T006; T014 depends on T013.
- **User Stories 1â€“5**: Depend on Phase 2. US1 is the MVP. US2 depends on the upload/model/query foundation but is independently testable after Phase 2; US3 depends on the document access foundation and notification conventions; US4 depends on task/dashboard integration points; US5 depends on activity data emitted by US1â€“US4.
- **Phase 8 Polish**: Depends on all desired stories, with T049â€“T051 able to begin once their corresponding services exist and T052â€“T055 after all story work.

### User Story Dependencies

- **US1 (P1)**: After Phase 2; no other story dependency.
- **US2 (P1)**: After Phase 2; uses US1 document records/storage but has an independent seeded-data test path.
- **US3 (P2)**: After Phase 2; uses US1/US2 service contracts and notification infrastructure.
- **US4 (P2)**: After Phase 2 and existing task/dashboard models; consumes document upload/access behavior.
- **US5 (P3)**: After Phase 2; meaningful end-to-end verification consumes activity emitted by US1â€“US4, though audit service/report tests can be developed in parallel.

### Parallel Opportunities

- Setup T002â€“T004 are parallelizable after T001.
- Foundational T007â€“T010 and T011 can be developed in parallel after the entity shape is agreed; T013 follows T005â€“T006.
- Within each story, all marked test tasks are parallelizable; independent model/service/UI test fixtures can run concurrently once their referenced contracts exist.
- After Phase 2, separate developers can work on US1, US2, US3, and US4 in parallel; US5 audit reporting can proceed in parallel with audit-producing flows.
- T049â€“T051 are independent validation suites and can run in parallel; T052â€“T055 are final cross-cutting checks.

## Parallel Execution Examples

### User Story 1

```text
Parallel: T015 model validation tests, T016 storage tests, T017 scanner tests,
          T018 compensation tests, T019 authorization tests, T020 schema tests
Then: T021 service -> T022/T023 UI and navigation -> T024 component fixture
```

### User Stories 2â€“5

```text
Parallel after Phase 2: US1 upload, US2 authorized discovery/access, US3 maintenance/sharing,
                        US4 task/dashboard integration, and US5 audit tests/report design
Within each story: tests -> service implementation -> pages/integration -> independent fixture
```

### Final Validation

```text
Parallel: T049 concurrency, T050 IDOR matrix, T051 file performance
Then: T052 quickstart acceptance, T053 security review, T054 migration compatibility, T055 documentation
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 and the blocking Phase 2 migration/authorization/storage foundation.
2. Complete Phase 3 (US1) and run its independent model, storage, scanner, transaction, authorization, schema, and UI tests.
3. Validate the quickstart upload/rejection/offline flow and stop for an MVP demo before adding discovery and maintenance.

### Incremental Delivery

1. Add US2 for authorized discovery and file access; validate IDOR and query performance.
2. Add US3 for maintenance and controlled sharing; validate replacement compensation, revocation, notifications, and deletion snapshots.
3. Add US4 for task/dashboard context and project notifications.
4. Add US5 for administrator audit reporting.
5. Complete Phase 8 security, performance, migration, offline, and documentation gates.

## Independent Test Criteria by Story

- **US1**: Authenticated employee uploads a supported <=25 MiB file with valid metadata, sees progress/success, finds it in an authorized list, and invalid/scanner-failed files leave no accessible content.
- **US2**: Seeded authorized/inaccessible records prove authorized browse/filter/search/sort/preview/download and prove unauthorized IDs, URLs, membership changes, and revoked shares reveal neither existence nor content.
- **US3**: Owner and project manager can edit/replace/share/revoke/delete as allowed; failed replacement retains prior content; recipients receive deduplicated notifications; deletion retains an administrator audit snapshot.
- **US4**: Authorized task viewer attaches/uploads with task-project consistency; dashboard shows only current-user recent five/count; project notifications reach eligible recipients without leakage.
- **US5**: Administrator sees complete immutable action records and requested aggregates after deletion; non-administrator access is denied without report or document leakage.
