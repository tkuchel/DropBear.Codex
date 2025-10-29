# Contributing to DropBear.Codex

## Branching
- `master` (or `main`): protected. All changes via PR.
- Features: `feat/<short-name>`; fixes: `fix/<short-name>`; chores/docs: `chore/` and `docs/`.

## Commits
- Prefer Conventional Commits (e.g., `feat: ...`, `fix: ...`, `docs: ...`, `chore: ...`).
- Keep PRs small and focused; include before/after context in the description.

## Tests
- Add/adjust unit tests for all public APIs.
- CI enforces build + coverage reporting.

## Releases
- Create a tag `v<version>` to publish NuGet packages automatically.

## Code style
- Follow `.editorconfig` and analyzers.
- Favor SOLID/SOC, DRY/KISS/YAGNI, and TDA across modules.
