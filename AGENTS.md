# Development and documentation

These rules apply to the whole repository.

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
