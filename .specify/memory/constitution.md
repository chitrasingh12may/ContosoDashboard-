<!--
Sync Impact Report
- Version change: 0.0.0 -> 1.0.0
- Modified principles: no prior constitution existed in repo; this establishes the initial project constitution
- Added sections: Training-First Scope, Local-First Architecture, Security-by-Design, Test-First Verification, Simplicity and Maintainability, Additional Constraints, Development Workflow, Governance
- Removed sections: none
- Templates requiring updates: .specify/templates/plan-template.md ✅ reviewed, no required changes; .specify/templates/spec-template.md ✅ reviewed, no required changes; .specify/templates/tasks-template.md ✅ reviewed, no required changes
- Deferred items: TODO(RATIFICATION_DATE): original adoption date not documented in the repo
-->

# ContosoDashboard Constitution

## Core Principles

### I. Training-First Scope
ContosoDashboard is explicitly a training-only application. Features MUST remain intentionally simple, local-first, and educational; no production claims or compliance guarantees are implied. Scope changes MUST prioritize learning value and safe, isolated demo behavior over operational complexity.

### II. Local-First Architecture
The application MUST prefer local, offline-supported implementations for data, identity, and storage, with service abstractions that allow later migration to cloud services. Business logic MUST remain decoupled from infrastructure; database and auth choices MUST be replaceable without rewriting domain behavior.

### III. Security-by-Design for Training
The project MUST enforce authentication, authorization, and data isolation with deliberate guardrails. All protected routes, services, and user-specific data access MUST validate identity and role checks. Security controls in the training app MUST teach defensible patterns, not insecure shortcuts.

### IV. Test-First Verification
Any behavioral change MUST be validated by a focused build or test cycle before completion. For feature work, the author MUST confirm the relevant command(s) pass in this environment; regression fixes MUST include evidence that the original problem is resolved and no new compile errors are introduced.

### V. Simplicity and Maintainability
The codebase MUST favor clear separation of concerns: models, data access, services, and UI pages stay distinct and readable. Prefer the smallest viable implementation that teaches the concept, and avoid speculative abstractions or framework churn without a documented need.

## Additional Constraints

The project MUST remain compatible with the .NET 10 SDK and current local developer tooling. SQLite is the default development database because it works on ARM64 Windows systems without SQL Server LocalDB dependencies. The project is intentionally not production-hardened and MUST NOT require cloud infrastructure or paid external services to run locally.

## Development Workflow

All changes MUST be kept aligned with the repository's training goals and the Spec Kit workflow. New work SHOULD be implemented through explicit requirements, plan checks, and validation runs before completion. Documentation updates are required whenever architecture, runtime assumptions, or setup instructions materially change.

## Governance

This constitution governs all repository decisions and supersedes informal shortcuts. Any amendment requires a documented rationale, a version bump, and a review of the active templates and runtime guidance for impacts. Changes that affect setup, security posture, or data layer choices MUST be validated with the relevant build or runtime command before acceptance.

Compliance review for each change MUST confirm:
- scope remains aligned to offline training use,
- security controls remain intentional and auditable,
- infrastructure dependencies stay local-first when possible,
- build or runtime verification is recorded.

**Version**: 1.0.0 | **Ratified**: TODO(RATIFICATION_DATE): original adoption date not documented in the repo | **Last Amended**: 2026-09-15
