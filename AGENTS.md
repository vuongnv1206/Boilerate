# Development and documentation

These rules apply to the whole repository.

- Read `docs/README.md`, `docs/roadmap.md` and the relevant feature plan before changing a feature.
- Develop documentation alongside implementation. Do not wait until the end of a feature to reconstruct its design and decisions.
- Before substantive implementation, create or update `docs/features/<feature>.md` using `docs/templates/feature-plan.md`. Record the goal, scope, ordered steps, acceptance criteria and pending decisions.
- For database changes, document cardinality, table ownership, columns, PK/FK, unique/check/index constraints, concurrency, deletion policy and migration strategy. Keep API contracts and operational guidance consistent with code.
- After each meaningful step, update the feature plan and add a dated entry in `docs/history/`. Record what changed, why, affected files, verification actually performed, remaining work and the next step. Link the entry from `docs/history/README.md`.
- Keep implemented, locally verified and accepted on a real deployment as distinct statuses. Configuration, mocks, skipped tests or a successful build do not establish production acceptance. Record failed and blocked checks honestly.
- When reconstructing earlier work, label the entry as retrospective; do not invent dates, commands or intermediate commit history.
- Every feature/fix commit should include its relevant documentation or history entry. Split commits by coherent behavior; keep tightly coupled model/API/migration changes together so intermediate commits remain buildable. Do not introduce known defects merely to create a separate fix commit.
- Keep test/build summaries in `docs/verification/`; keep raw generated logs, TRX files, packages, uploads, keys and secrets out of Git and template source.
- Update `docs/roadmap.md` when scope or acceptance status changes. Keep `plan.md` as a navigation entry rather than a second roadmap with conflicting statuses.
