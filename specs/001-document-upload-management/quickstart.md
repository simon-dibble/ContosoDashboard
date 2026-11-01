# Quickstart and Validation Guide

## Prerequisites

- .NET 9 SDK, SQL Server LocalDB, and the repository's existing dependencies.
- A clean training database or a backup of the existing `ContosoDashboard` LocalDB database.
- Run from `C:\Users\236247\Development\source\training\ContosoDashboard\ContosoDashboard`.

## Database and offline run

1. Generate/apply the named EF migration from the implementation phase:
   `dotnet ef migrations add AddDocumentManagement`.
2. Apply it locally with `dotnet ef database update` (or the documented startup migration path).
3. Confirm the application no longer relies on `EnsureCreated()` for an already-migrated
   database and that seed users/projects remain available.
4. Configure a private storage root outside `wwwroot`; do not commit uploaded files.
5. Run `dotnet run`, open the local URL, and log in using the existing mock users from `README.md`.
   Disable network access to verify the core flow remains usable.

## Functional acceptance

- Upload one supported PDF/image/text/Office file <=25 MB with title/category; observe per-file
  progress, success, list metadata, and dashboard count/recent-five update.
- Upload an unsupported extension, >25 MB file, empty title, invalid category, duplicate/empty
  tags, and invalid date range; each receives a clear rejection and no discoverable document.
- Trigger the deterministic scanner sentinel; verify rejection, no download/preview, and no
  accessible private file.
- Browse My Documents and project documents; sort by title/date/category/size, filter category/
  project/date, search title/description/tags/uploader/project, and verify empty/500/no-match
  states.
- Preview PDF/JPEG/PNG in browser and download every supported type with authorization. Verify
  unsupported preview types still download.
- Edit metadata and replace a file; force validation/scanner/storage failure and verify the
  previous valid content remains available.
- Attach an existing document and upload from an authorized task; verify the task project
  association.
- Share with a selected user and eligible department/project team; verify deduplicated grants,
  Shared with Me, notifications, and revocation behavior.
- Confirm project-member notifications for new project documents and the dashboard integration.
- Delete as owner/project manager after confirmation; verify content/document removal and retained
  administrator audit snapshot.

## Authorization and IDOR validation

Using the seeded users, test owner, project member, team lead, project manager, administrator,
non-member, and revoked-share cases. Attempt direct document IDs and guessed download/preview
URLs for every operation. Expected result is the same not-found/denied behavior without title,
metadata, existence, storage path, or search-result leakage. Non-administrators must not access
audit pages/reports. Confirm membership and shares are re-evaluated after revocation.

## Automated and integration tests

The later implementation tasks should add tests for:

- model validation, allowed categories/types, 25 MiB boundary, tag normalization, and UTC values;
- `IFileStorage` path traversal/opaque-name and cleanup behavior;
- deterministic scanner pass/reject/error fail-closed behavior;
- transaction compensation for storage, scan, metadata, and replacement failures;
- authorization matrix and IDOR query ordering for each role/relationship;
- notification recipients, deduplication, and audit snapshots after delete;
- EF migration, indexes, query filters, and task/project association;
- Blazor component upload progress, per-file errors, preview/download, and empty states.

## Performance and security checks

Seed 500 authorized documents plus inaccessible documents. Measure p95 list/search (<2 s),
upload <=25 MB (<30 s under typical local test conditions), and PDF/image preview (<3 s).
Run concurrency checks for duplicate names and simultaneous replacement. Review logs to ensure
they contain identifiers/outcomes but never file bytes, secrets, or private paths. Scan dependency
and build output with the repository's available tooling and record unavailable checks.

## Migration path

Back up LocalDB before applying the migration. Keep the metadata/service contracts stable while
replacing only the `IFileStorage` adapter with a production object-store implementation and
`IMalwareScanner` with a real scanner. Replace mock authentication separately with a real identity
provider; do not put production credentials or network requirements into this training project.
