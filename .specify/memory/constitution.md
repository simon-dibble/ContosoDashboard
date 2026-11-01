<!--
Sync Impact Report
- Version change: 1.0.0 → 1.0.1
- Modified sections: Technology and Security Constraints updated for the repository's .NET 9
  target
- Added sections: Technology and Security Constraints; Development Workflow and Quality Gates
- Removed sections: none
- Follow-up TODOs: RATIFICATION_DATE requires confirmation of the original adoption date.
-->

# ContosoDashboard Constitution

## Core Principles

### I. Offline-First Training Scope
ContosoDashboard MUST remain usable locally and offline for its training purpose. New
functionality MUST avoid required external services, cloud credentials, or network access unless
the project explicitly documents a local fallback and a migration path. This preserves reliable
classroom use and keeps the training environment safe and repeatable.

### II. Secure-by-Default Access
Every protected page, endpoint, and service operation MUST enforce authentication and
authorization at the appropriate boundary. Code handling user or resource identifiers MUST
prevent cross-user access (including IDOR), validate input, and avoid exposing secrets or
sensitive data in responses and logs. The mock identity system MUST remain clearly identified as
training-only. These rules ensure that security lessons remain visible in every feature.

### III. Layered and Replaceable Architecture
Business logic MUST remain separated from UI, persistence, and infrastructure concerns through
the established Models, Services, Data, and Pages layers. Infrastructure dependencies MUST use
interfaces and dependency injection where replacement is a stated design goal. A change that
crosses a layer MUST document its boundary and preserve the ability to replace local
implementations with production-oriented adapters.

### IV. Testable, Reviewable Changes
Each change MUST include tests appropriate to its risk and MUST preserve existing behavior unless
the change explicitly revises that behavior. Authorization, service boundaries, data access, and
integration contracts MUST receive focused verification when they change. Pull requests MUST
state the validation performed and identify any intentionally untested behavior so reviewers can
make an informed decision.

### V. Simplicity and Traceability
The simplest design that satisfies the requirement MUST be preferred. Features MUST avoid
speculative abstractions, duplicate sources of truth, and unnecessary dependencies. User-visible
and security-relevant behavior MUST be explainable through clear names, concise documentation,
and structured diagnostics that do not disclose sensitive data. This keeps the training code
understandable and makes failures diagnosable.

## Technology and Security Constraints

The application targets ASP.NET Core 9.0 with Blazor Server, Entity Framework Core, SQL Server
LocalDB, and Bootstrap 5.3. Changes MUST remain compatible with the repository's supported
toolchain unless the constitution is amended. Local development MUST continue to work without
cloud services. Production migration guidance MUST use approved abstractions rather than
introducing production credentials or services into the training implementation. Security
headers, secure cookie settings, authorization attributes or policies, service-level checks, and
input validation MUST be retained when relevant code is changed.

## Development Workflow and Quality Gates

Work MUST begin with a sufficiently precise specification for behavior, affected users, security
impact, and acceptance criteria. Implementation MUST be reviewed against that specification and
this constitution. Before merging, contributors MUST run the relevant automated tests and
validation checks, manually verify changed user flows when practical, and record failures or
environment limitations. Changes that alter data models, authorization, shared contracts, or
infrastructure boundaries MUST include an explicit migration or compatibility assessment.

## Governance

This constitution takes precedence over conflicting project practices. Amendments MUST be made
through the constitution workflow, preserve the required template structure, include a Sync
Impact Report, and explain the affected principles, compatibility impact, and migration needs.
The amendment MUST be reviewed before commit.

Constitution versions follow semantic versioning: MAJOR for backward-incompatible governance
changes or removed/redefined principles, MINOR for new principles or materially expanded
requirements, and PATCH for clarifications and non-semantic corrections. The Last Amended date
MUST change whenever the governed content changes. Every implementation review MUST check
compliance with the principles, constraints, and quality gates above; any exception MUST be
documented with its scope, rationale, owner, and expiry or follow-up plan. The temporary Sync
Impact Report MUST be removed before the amended file is committed.

**Version**: 1.0.1 | **Ratified**: TODO(RATIFICATION_DATE): confirm original adoption date | **Last Amended**: 2026-11-02
