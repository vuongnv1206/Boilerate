# Project-wide working rules

Use these principles across languages, frameworks and product domains. Repository-specific instructions provide the local conventions; explicit user requirements determine the task's scope.

## 1. Establish the intended result

- Read the relevant implementation and documentation before proposing changes. Separate observed behavior, requirements and assumptions.
- Explain decisions that affect behavior, data or compatibility. Ask when an unresolved choice would materially change the result; handle routine, reversible choices using existing conventions and state any useful assumption.
- For substantial work, give a short sequence of steps with an observable completion check for each.

## 2. Keep the solution proportionate

- Implement the requested behavior with the least complexity that remains clear and correct.
- Add abstractions, dependencies and configuration only when the current task needs them. Discuss meaningful tradeoffs without building speculative features.
- Prefer the repository's established patterns. Address failure modes supported by the actual interfaces and operating conditions.

## 3. Limit changes to the task

- Make every edited file serve the intended result. Keep unrelated cleanup, formatting and refactoring out of the change.
- Preserve existing user work. Remove unused code introduced by this change; report unrelated issues separately unless they prevent completing the task.
- Review the final diff for accidental edits, generated files and sensitive data. When committing, group changes into coherent, reviewable steps that keep the project consistent.

## 4. Demonstrate the outcome

- Choose checks that exercise the affected behavior and match the risk. For a bug, reproduce the failure when practical; for a small documentation change, direct inspection may be sufficient.
- Run the relevant checks, investigate failures and complete the authorized work. State blockers and remaining uncertainty precisely.
- Update affected documentation alongside the change. Report what was implemented, what actually ran and what still requires verification; never present a mock, skipped check or unrun command as successful evidence.

Reference: independently worded guidance inspired by [multica-ai/andrej-karpathy-skills — CLAUDE.md](https://github.com/multica-ai/andrej-karpathy-skills/blob/main/CLAUDE.md), reviewed on 2026-10-19.
