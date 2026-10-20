# Project instructions

These rules apply to the whole repository.

## General working principles

Use these principles across languages, frameworks and product domains. Repository-specific instructions provide the local conventions; explicit user requirements determine the task's scope.

### 1. Establish the intended result

- Read the relevant implementation and documentation before proposing changes. Separate observed behavior, requirements and assumptions.
- Explain decisions that affect behavior, data or compatibility. Ask when an unresolved choice would materially change the result; handle routine, reversible choices using existing conventions and state any useful assumption.
- For substantial work, give a short sequence of steps with an observable completion check for each.

### 2. Keep the solution proportionate

- Implement the requested behavior with the least complexity that remains clear and correct.
- Add abstractions, dependencies and configuration only when the current task needs them. Discuss meaningful tradeoffs without building speculative features.
- Prefer the repository's established patterns. Address failure modes supported by the actual interfaces and operating conditions.

### 3. Limit changes to the task

- Make every edited file serve the intended result. Keep unrelated cleanup, formatting and refactoring out of the change.
- Preserve existing user work. Remove unused code introduced by this change; report unrelated issues separately unless they prevent completing the task.
- Review the final diff for accidental edits, generated files and sensitive data. When committing, group changes into coherent, reviewable steps that keep the project consistent.

### 4. Demonstrate the outcome

- Choose checks that exercise the affected behavior and match the risk. For a bug, reproduce the failure when practical; for a small documentation change, direct inspection may be sufficient.
- Run the relevant checks, investigate failures and complete the authorized work. State blockers and remaining uncertainty precisely.
- Update affected documentation alongside the change. Report what was implemented, what actually ran and what still requires verification; never present a mock, skipped check or unrun command as successful evidence.

Reference: independently worded guidance inspired by [multica-ai/andrej-karpathy-skills — CLAUDE.md](https://github.com/multica-ai/andrej-karpathy-skills/blob/main/CLAUDE.md), reviewed on 2026-10-19.

## Development and documentation

- Read `docs/README.md`, the owning area's roadmap (`docs/backend/roadmap.md` or `docs/frontend/roadmap.md`) and the relevant feature plan before changing a feature. For shared changes, read `docs/shared/README.md` and the affected areas' plans.
- Develop documentation alongside implementation. Do not wait until the end of a feature to reconstruct its design and decisions.
- Before substantive implementation, create or update `docs/<area>/features/<feature>.md` using `docs/shared/templates/feature-plan.md`, where `<area>` is `backend`, `frontend` or `shared`. Record the goal, scope, ordered steps, acceptance criteria and pending decisions. Cross-area features use a shared coordination plan linked to the BE/FE plans; do not duplicate contracts or progress.
- For database changes, document cardinality, table ownership, columns, PK/FK, unique/check/index constraints, concurrency, deletion policy and migration strategy. Keep API contracts and operational guidance consistent with code.
- After each meaningful step, update the feature plan and add a dated entry in `docs/<area>/history/`. Record what changed, why, affected files, verification actually performed, remaining work and the next step. Link the entry from that area's `history/README.md`, creating the index when recording the first entry.
- Keep implemented, locally verified and accepted on a real deployment as distinct statuses. Configuration, mocks, skipped tests or a successful build do not establish production acceptance. Record failed and blocked checks honestly.
- When reconstructing earlier work, label the entry as retrospective; do not invent dates, commands or intermediate commit history.
- Every feature/fix commit should include its relevant documentation or history entry. Split commits by coherent behavior; keep tightly coupled model/API/migration changes together so intermediate commits remain buildable. Do not introduce known defects merely to create a separate fix commit.
- Keep test/build summaries in `docs/<area>/verification/`; keep raw generated logs, TRX files, packages, uploads, keys and secrets out of Git and template source.
- Update the owning area's roadmap when scope or acceptance status changes. Keep `docs/README.md` and `plan.md` as navigation entries rather than duplicate roadmaps with conflicting statuses. Shared business decisions live in `docs/shared/`; the BE API/database contracts live in `docs/backend/` and FE references them.
