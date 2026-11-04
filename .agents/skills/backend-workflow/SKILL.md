---
name: backend-workflow
description: Implement substantial Boilerate .NET API, business-rule or database changes with the repository contracts and SQL Server/PostgreSQL migration conventions. Use for backend features, not frontend UI or routine documentation edits.
---

# Backend workflow

Read ../../../AGENTS.md and ../../../docs/README.md from this file's directory. Read the owning backend feature and roadmap before editing. Contracts live in ../../../docs/backend/; shared business decisions live in ../../../docs/shared/.

Keep API, domain, application and persistence changes in their existing owning layers under src/. For material business/API/database changes, update the feature plan using docs/shared/templates/feature-plan.md relative to the repo root. Document ownership, cardinality, constraints, concurrency, deletion and migration strategy when schema changes. Preserve authorization, transaction and idempotency behavior established by the affected source.

Use SQL Server and PostgreSQL migration/test conventions already implemented in the repo. Run checks appropriate to changed behavior; do not imply an unconfigured provider or deployment was tested. Update contracts in place, keep raw logs in ignored artifacts and report remaining acceptance limits.
