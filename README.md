# ContosoDashboard

The ContosoDashboard application is intended for TRAINING PURPOSES ONLY.

The ContosoDashboard repository contains the starter code project for training that teaches Spec-Driven Development (SDD) using the GitHub Spec Kit. ContosoDashboard is a fictional application created solely for educational purposes.

- The project codebase is NOT intended for use in production environments.
- The project architecture is NOT intended as a model for production applications.
- The project is NOT actively maintained and may contain bugs or security vulnerabilities.
- The project is provided "as-is" without warranties or support of any kind.
- The project implements mock authentication and authorization for training purposes only.
- The project does NOT implement cloud integration or external service dependencies (local only and offline to maximize training availability).
- The project demonstrates good coding practices, simplified for a training context, with known and documented limitations.

## 🔒 Security Features (Training Implementation)

This application includes a **mock authentication system** designed for training without external dependencies:

- ✅ Cookie-based authentication (8-hour sliding expiration)
- ✅ Claims-based identity with user roles
- ✅ Razor Pages for login/logout (proper HTTP request handling)
- ✅ Custom authentication state provider for Blazor Server integration
- ✅ Authorization enforcement on all protected pages (`[Authorize]` attribute)
- ✅ Role-based access control (RBAC) with hierarchical permissions
- ✅ Service-level security to prevent unauthorized data access
- ✅ IDOR (Insecure Direct Object Reference) protection
- ✅ Defense in depth (middleware, page attributes, service checks)
- ✅ Security headers (CSP, X-Frame-Options, X-XSS-Protection, etc.)
- ✅ Cookie security with sliding expiration
- ✅ User isolation - each user sees only their authorized data
- ✅ No external services required (suitable for offline training)

**Note**: The mock authentication is suitable for training only. Production deployments require proper identity providers with password hashing, MFA, OAuth 2.0/OpenID Connect, and compliance with security standards (WCAG 2.1, TLS encryption, audit logging).

### Mock Login System

**Available Users** (no password required - select from dropdown):

| Display Name | Email | Role | Department |
|-------------|-------|------|------------|
| System Administrator | `admin@contoso.com` | Administrator | IT |
| Camille Nicole | `camille.nicole@contoso.com` | Project Manager | Engineering |
| Floris Kregel | `floris.kregel@contoso.com` | Team Lead | Engineering |
| Ni Kang | `ni.kang@contoso.com` | Employee | Engineering |

**Login Process:**

1. Navigate to `/login` (automatic redirect if not authenticated)
2. Select a user from the dropdown
3. Click "Login" - you'll be redirected to the dashboard as that user

⚠️ **Important:** This mock authentication system is for **training only**. Production applications should use Azure AD, Identity Server, Auth0, or similar identity providers with proper password hashing, MFA, and OAuth 2.0/OpenID Connect.

## Overview

## Document management migration and private storage

Document uploads use the named EF migration `AddDocumentManagement` and a private local
storage root configured by `DocumentStorage:RootPath` (the default is under `%LOCALAPPDATA%`,
never `wwwroot`). The maximum file size is 25 MiB. Supported types are PDF, Word, Excel,
PowerPoint, text, JPEG, and PNG. The offline scanner is a deterministic **training-only**
gate and is not an antivirus product; replace `IMalwareScanner` for production.

Before upgrading an existing training database, take a LocalDB backup. This repository
started with `EnsureCreated`, so an existing database without `__EFMigrationsHistory` needs
to be backed up and recreated (or manually baselined by an instructor) before running
`dotnet ef database update`; the generated migration is a complete baseline plus document
tables and cannot be applied over an untracked `EnsureCreated` schema. Fresh databases are
created with `dotnet ef database update`, preserving the existing seed users, projects,
tasks, members, and notifications. Roll back by restoring the backup; do not mix
`EnsureCreated` and migrations for the same database.

Core document behavior is offline-first. `IFileStorage` and `IMalwareScanner` are replaceable
seams for a future production object store and scanner, without adding credentials or network
requirements to this training application. Uploads are authorization-checked server-side,
stored under opaque names, and never expose filesystem paths.

ContosoDashboard is built using ASP.NET Core 9.0 with Blazor Server and provides a centralized platform for:

- Task management and tracking
- Project oversight and collaboration
- Team coordination
- Notifications and announcements
- User profile management

## Features

### ✅ Implemented Features

- **Mock Authentication System**: User selection login, cookie-based auth, claims-based identity
- **Authorization Enforcement**: `[Authorize]` attributes on all protected pages, role-based policies
- **Dashboard Home Page**: Personalized dashboard with summary cards showing active tasks, due dates, projects, and notifications
- **Task Management**: View, filter, sort, and update tasks with priority levels and status tracking
- **Project Management**: Browse projects with completion percentages, team members, and status indicators
- **Project Details**: Comprehensive project view with task list, team members, and project statistics
- **Team Directory**: Browse team members by department with status, roles, and contact information
- **Notifications Center**: View and manage all notifications with read/unread status and priority badges
- **User Profile**: Update personal information, availability status, and notification preferences
- **Service-Level Security**: Authorization checks prevent IDOR vulnerabilities
- **Data Models**: Complete entity framework models for Users, Tasks, Projects, Notifications, and Announcements
- **Business Services**: Service layer for all core functionality (Tasks, Projects, Users, Notifications, Dashboard)
- **Database Context**: EF Core DbContext with relationships, indexes, and seed data

### 🔧 Technical Stack

- **Framework**: ASP.NET Core 9.0
- **UI**: Blazor Server
- **Database**: SQL Server LocalDB with Entity Framework Core
- **Authentication**: Cookie-based mock authentication for training (Azure AD/Microsoft Entra ID ready)
- **Authorization**: Claims-based identity with role-based access control
- **Styling**: Bootstrap 5.3 with Bootstrap Icons
- **Architecture**: Clean separation of concerns with Models, Services, Data, and Pages layers
- **Security**: IDOR protection, service-level authorization, `[Authorize]` attributes

## Architecture Principles

### Offline-First with Cloud Migration Path

This training application follows an **offline-first architecture** with abstraction layers that enable seamless migration to Azure services:

**Current Implementation (Training/Offline):**
- **Database**: SQL Server LocalDB (offline development database)
- **File Storage**: Local filesystem for any file-based features
- **Authentication**: Cookie-based mock authentication

**Production Migration Path:**
- **Database**: Azure SQL Database (replace connection string, no code changes)
- **File Storage**: Azure Blob Storage (swap `IFileStorageService` implementation)
- **Authentication**: Microsoft Entra ID (replace authentication middleware)

**Key Design Pattern - Infrastructure Abstraction:**

All infrastructure dependencies use **interface abstractions** to enable switching between local and cloud implementations:

```csharp
// Example: File storage abstraction
public interface IFileStorageService
{
    Task<string> UploadAsync(Stream fileStream, string fileName, string contentType);
    Task DeleteAsync(string filePath);
    Task<Stream> DownloadAsync(string filePath);
}

// Training: LocalFileStorageService (uses System.IO)
// Production: AzureBlobStorageService (uses Azure SDK)
// Swap via dependency injection - no business logic changes required
```

**Benefits of This Approach:**
- Students learn proper abstraction patterns and dependency injection
- Training works offline without Azure subscriptions or cloud costs
- Migration to production requires only configuration and implementation swaps
- Business logic remains unchanged during cloud migration
- Demonstrates industry-standard separation of concerns

**File upload best practice:** When implementing file uploads, generate unique file paths (using GUID) before database insertion to prevent duplicate key violations and orphaned records.

## Getting Started

### Prerequisites

- .NET 9.0 SDK or later
- SQL Server LocalDB
- Visual Studio 2022 or Visual Studio Code

### Quick Start

1. **Navigate to the project directory**:

   ```powershell
   cd ContosoDashboard
   ```

2. **Run the application** (database will be created automatically):

   ```powershell
   dotnet run
   ```

3. **Open your browser** to `http://localhost:5000`

4. **Login** - Select any user from the dropdown (no password required)

The application automatically creates and seeds the database on first run with sample users, projects, tasks, and announcements.

### Testing Security Features

#### Test 1: Authentication Required

- Open browser in incognito mode
- Try to navigate to `https://localhost:xxxx/tasks`
- Expected: Redirect to `/login`

#### Test 2: User Isolation

- Login as "Ni Kang"
- Note the tasks and projects shown
- Logout and login as "Floris Kregel"
- Expected: Different tasks and projects displayed

#### Test 3: IDOR Protection

- Login as "Ni Kang" and view a project (note the ID in URL)
- Logout and login as "System Administrator"
- Try to access the same project by URL
- Expected: Access only if you're a member

#### Test 4: Role-Based Features

- Login as different users to see varying levels of access
- Employee: View assigned tasks, update status
- Project Manager: Manage projects, assign tasks
- Administrator: Full system access

## Project Structure

```plaintext
ContosoDashboard/
├── Data/
│   └── ApplicationDbContext.cs      # EF Core database context
├── Models/
│   ├── User.cs                      # User entity with roles
│   ├── TaskItem.cs                  # Task entity
│   ├── Project.cs                   # Project entity
│   ├── TaskComment.cs               # Task comments
│   ├── Notification.cs              # User notifications
│   ├── ProjectMember.cs             # Project team members
│   └── Announcement.cs              # System announcements
├── Services/
│   ├── IUserService.cs / UserService.cs
│   ├── ITaskService.cs / TaskService.cs
│   ├── IProjectService.cs / ProjectService.cs
│   ├── INotificationService.cs / NotificationService.cs
│   ├── IDashboardService.cs / DashboardService.cs
│   └── CustomAuthenticationStateProvider.cs  # Blazor Server auth integration
├── Pages/
│   ├── Index.razor                  # Dashboard home page
│   ├── Login.cshtml / Login.cshtml.cs  # Mock authentication login (Razor Page)
│   ├── Logout.cshtml / Logout.cshtml.cs  # Logout handler (Razor Page)
│   ├── Tasks.razor                  # Task list and management
│   ├── Projects.razor               # Project list view
│   ├── ProjectDetails.razor         # Individual project details
│   ├── Team.razor                   # Team member directory
│   ├── Notifications.razor          # Notification center
│   ├── Profile.razor                # User profile page
│   └── _Host.cshtml                 # Blazor Server host page
├── Shared/
│   ├── MainLayout.razor             # Main layout template
│   └── NavMenu.razor                # Navigation sidebar
├── wwwroot/
│   └── css/site.css                 # Custom styles
├── Program.cs                       # Application entry point
├── appsettings.json                 # Configuration
└── ContosoDashboard.csproj          # Project file
```

## Configuration

### Database Connection

The default connection string in `appsettings.json` uses SQL Server LocalDB:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ContosoDashboard;Trusted_Connection=True;MultipleActiveResultSets=true"
}
```

Update this if using a different SQL Server instance.

### Production Authentication Guidance

This training application uses mock authentication. For production applications, you would need to:

- Implement proper identity providers (Azure AD, Identity Server, Auth0)
- Add password hashing and salting (e.g., bcrypt, PBKDF2)
- Enable multi-factor authentication (MFA)
- Implement OAuth 2.0/OpenID Connect protocols
- Add rate limiting and account lockout policies
- Implement comprehensive audit logging
- Use secure session management with idle timeouts
- Implement password complexity requirements and rotation policies

See [Microsoft's ASP.NET Core Security documentation](https://docs.microsoft.com/aspnet/core/security/) for production implementation guidance.

### User Roles

The application supports four role levels with hierarchical permissions:

- **Employee**: View and update assigned tasks, view projects where member, manage own profile
- **TeamLead**: All Employee permissions plus view team member activities
- **ProjectManager**: All TeamLead permissions plus create/manage projects, assign tasks
- **Administrator**: Full system access including all administrative functions

## Sample Data

The application includes pre-seeded data for testing:

**Users** (all available for mock login):

- `admin@contoso.com` - System Administrator (Administrator role)
- `camille.nicole@contoso.com` - Camille Nicole (Project Manager role)
- `floris.kregel@contoso.com` - Floris Kregel (Team Lead role)
- `ni.kang@contoso.com` - Ni Kang (Employee role)

**Project**:

- "ContosoDashboard Development" with 3 sample tasks in various states

## Application Pages

| Page | Route | Description | Auth Required |
|------|-------|-------------|---------------|
| Login | `/login` | User selection for mock auth | No |
| Dashboard | `/` | Summary, announcements, quick actions | Yes |
| Tasks | `/tasks` | View and manage your tasks | Yes |
| Projects | `/projects` | View your projects | Yes |
| Project Details | `/projects/{id}` | Detailed project view | Yes (member only) |
| Team | `/team` | View team members | Yes |
| Notifications | `/notifications` | Manage notifications | Yes |
| Profile | `/profile` | Edit your profile | Yes |
| Logout | `/logout` | End session and clear cookies | Yes |

## Key Functionalities

### Dashboard (Home Page)

- Summary cards with real-time metrics
- Active announcements display
- Quick action links
- Recent notifications feed

### Task Management

- Filter by status, priority, and project
- Quick status updates via dropdown
- Priority-based color coding
- Overdue task highlighting

### Project Management

- Project cards with progress bars
- Completion percentage calculation
- Team member visibility
- Status badges

### User Profile

- Profile information editing
- Availability status management
- Notification preferences
- Display initials when no photo is set

## Troubleshooting

### Can't Login

- Ensure database is created (run `dotnet run` to auto-create)
- Check that seeded users exist in database
- Clear browser cookies and try again

### Redirected to Login After Login

- Check browser cookies are enabled
- Clear browser cache and cookies
- Try incognito/private mode

### Can't Access a Page

- Verify you're logged in (user name shown in top-right)
- Check if your role has permission for that resource
- Verify you're a member of the project/task you're trying to access

### Database Issues

**Option 1: Recreate via LocalDB**

```powershell
sqllocaldb stop mssqllocaldb
sqllocaldb delete mssqllocaldb
# Then run the application - database will be recreated automatically
```

**Option 2: Using EF Tools**

- Delete database: `dotnet ef database drop --force`
- Recreate: Run application (auto-creates with seed data)

**Note**: The application uses `Database.Migrate()` at startup, so a fresh database is
automatically migrated and seeded. An older database created with `EnsureCreated()` must follow
the backup/recreate or instructor-managed baseline procedure described above.

## Security Concepts and Patterns

This training application demonstrates the following security concepts and patterns:

1. **Authentication Patterns** - How to implement and configure authentication
2. **Authorization Enforcement** - Using attributes and policies
3. **Claims-Based Identity** - Working with user claims
4. **IDOR Prevention** - Service-level authorization checks
5. **Security Best Practices** - Defense in depth, least privilege
6. **ASP.NET Core Security** - Industry-standard patterns and middleware

## Known Limitations (Training Context)

This is a **training application**, not production code. Known limitations include:

- **Mock authentication**: No real passwords - anyone can select any user account
- **No rate limiting**: Vulnerable to brute force attacks and denial of service
- **No audit logging**: Security events (login, failed auth, data changes) are not logged
- **Simplified input validation**: Production apps need more comprehensive validation
- **No session timeout warnings**: Users aren't warned before session expiration
- **CSP includes unsafe directives**: `'unsafe-inline'` and `'unsafe-eval'` required for Blazor Server but not ideal for security
- **No email verification**: User emails are not validated
- **No account lockout**: Failed login attempts don't trigger account locks

These limitations are **intentional** for training purposes to keep the application simple and self-contained. Production applications must address all of these security concerns.

## Code Quality Features

The application demonstrates good coding practices:

- Database indexes on frequently queried fields for performance
- Async/await pattern throughout for non-blocking operations
- Entity Framework Core with eager loading (`.Include()`) to prevent N+1 query problems
- Clean separation of concerns (Models, Services, Data, Pages)
- Dependency injection for loose coupling and testability

## Document-management validation (T052, this pass)

This updates/replaces the earlier 2026-11-02 validation note and reflects the state after
completing the remaining feasible T026/T030-T033/T037/T041-T045/T048-T052 tasks from
`specs/001-document-upload-management/tasks.md`.

### Build and automated tests

- `dotnet build ContosoDashboard/ContosoDashboard.csproj` - succeeded (only four
  pre-existing nullable-reference warnings in `TaskService.cs`/`DocumentService.cs`; no errors).
- `dotnet test ContosoDashboard.Tests/ContosoDashboard.Tests.csproj` - **98 tests passed, 0
  failed**, including new coverage added in this pass for stream access (T026), the browse/
  security UI fixture (T030), edit/replacement (T031), confirmed deletion (T033), the US3
  component fixture (T037), task upload/attach + document display (T041), project-document
  notifications (T042), the task/dashboard component fixture (T043), append-only audit
  recording for upload/download/share/delete (T044), aggregate audit reporting by file type
  and uploader (T045), the administrator audit UI fixture (T048), true concurrent
  upload/replace races (T049), a full IDOR authorization matrix across every
  `DocumentOperation` and role (T050, new `ContosoDashboard.Tests/Security/DocumentIdorSecurityTests.cs`),
  and 25 MiB upload / PDF-image preview performance bounds (T051).

### Fresh-database migration (never touches the existing database)

- `dotnet ef database update` was run with `ConnectionStrings__DefaultConnection` pointed at a
  **brand-new** LocalDB database, `ContosoDashboard_T052Validation` - the existing
  `ContosoDashboard` database was never opened or modified by this validation pass.
- The migration applied cleanly: all `Document*` tables (`Documents`, `DocumentShares`,
  `DocumentActivities`, `DocumentTaskAssociations`) and their indexes were created, and the
  `__EFMigrationsHistory` table recorded the `AddDocumentManagement` migration.
- A clean-seed check confirmed the mock `Users`/`Projects` seed data (`HasData`) was applied
  by the migration itself: 4 users and 1 project were present immediately after `database update`,
  with no separate seeding step required.
- `dotnet run` was started against that same fresh database (`ASPNETCORE_URLS` + connection
  string overridden via environment variables) and `/` and `/login` both returned HTTP 200,
  confirming the application starts and serves pages against a freshly migrated database.

### Manual/offline checks that could not be completed in this environment

The following items from `quickstart.md`'s functional-acceptance and authorization/IDOR
validation sections require a real browser and/or human judgement and were **not** performed
in this automated pass - they remain open manual follow-ups:

- Browser-driven upload progress UI, drag/drop, and visual preview rendering (PDF iframe /
  image `<img>`) were **not** visually verified; only their presence in markup and their
  service-layer behavior are covered by automated tests.
- **Offline/no-network run**: the application itself needs no network access (local SQL
  Server LocalDB, local private file storage, and the deterministic in-process malware
  scanner), but `Pages/_Host.cshtml` and `Pages/Login.cshtml` load Bootstrap CSS/JS and
  Bootstrap Icons from the `cdn.jsdelivr.net` CDN. In a fully offline environment the pages
  will still render and function (forms/buttons work), but styling and icon glyphs will be
  missing until those assets are vendored locally. This is a pre-existing condition of the
  base application, not something introduced by the document-management feature, and is
  recorded here rather than silently fixed because vendoring front-end assets is outside this
  feature's scope.
- A full manual walk-through of every acceptance bullet in `quickstart.md` (e.g., clicking
  through the UI as each seeded role, dragging files, confirming toast/notification text) was
  not performed; this requires a human (or browser-automation) session and is left for a
  follow-up manual QA pass.
- Dependency/build-output security scanning tools (e.g., `dotnet list package
  --vulnerable`, container or SAST scanners) were not available/configured in this sandbox and
  were not run; only the automated test suite and the source-level authorization review below
  were performed.

### Known, intentionally-unchecked task

- **T032** (`DocumentSharingTests.cs`) is left unchecked in `tasks.md`. Automated tests exist
  and pass for everything the production code actually implements (selected-user sharing with
  deduplication, revoke, Shared with Me active/revoked filtering, and owner/manager-only
  authorization). Two parts of T032's acceptance criteria have **no corresponding production
  implementation** anywhere in the codebase - automatic department/project-team membership
  expansion when sharing, and per-user notification recipients/preferences - so they cannot be
  tested honestly without first building new (out-of-scope) domain features. See the
  explanatory note on T032 in `tasks.md` and the comment at the top of
  `DocumentSharingTests.cs`.

The document feature remains offline-first: local private storage, the deterministic
training scanner, and replaceable `IFileStorage`/`IMalwareScanner` seams require no
network or cloud credentials for their actual logic (only the CDN-hosted front-end styling
noted above is network-dependent). An existing `EnsureCreated` database must still be
backed up and recreated/baselined by an instructor before migration (see the migration
section above).

The implementation review confirmed that document pages are `[Authorize]` protected,
document services authorize before loading identifiers, and UI/logging code does not
render storage names, filesystem paths, uploaded bytes, or credentials. The audit page
also applies an administrator role boundary and repeats the service authorization check.
This is a source review only; it is not a substitute for a penetration test.
