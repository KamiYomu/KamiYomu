# KamiYomu — AGENTS.md

Instructions for any AI coding agent (Claude, Copilot, Cursor, etc.) working in this repository.
Act as a senior software architect: clear boundaries, testable abstractions, minimal and
purposeful changes.

## Stack

- .NET 8 Razor Pages, plugin-based crawler agent architecture.
- **LiteDB** — main application storage (library, chapters, reading state).
- **SQLite** — Hangfire backend only (`src/KamiYomu.Web/Worker`), never domain data.
- Tests: `src/KamiYomu.Web.Tests` (xUnit + Moq), referencing `KamiYomu.Web.csproj`.

## Core rules

1. **Never commit directly to `main` or `develop`.** All work happens on a branch and lands via PR.
2. **New work branches from `develop`**, prefix `feature/<name>`; PRs target `develop`. Releases
   flow `develop` → `main` (see [Branching](#branching)).
3. Write testable code: depend on interfaces, not concrete LiteDB/Hangfire/SQLite types; use
   constructor injection over static/service-locator access.
4. **Run the test suite before calling work done**: `dotnet test src/KamiYomu.sln`
   (or scope to `src/KamiYomu.Web.Tests/KamiYomu.Web.Tests.csproj`). All tests must pass unless the
   user explicitly accepts failures/skips.
5. For hot paths (crawler jobs, downloads, DB queries, page rendering), the change must not
   regress performance — measure or reason about it explicitly, and flag suspected regressions.
6. Keep changes surgical and scoped to the request; don't refactor unrelated code.
7. No secrets in source, config, or commit history.

## Branching

| Prefix      | Branches from | Purpose                       | Merges into |
| ----------- | ------------- | ------------------------------ | ----------- |
| `feature/`  | `develop`     | New functionality              | `develop`   |
| `fix/`      | `develop`     | Bug fixes                      | `develop`   |
| `hotfix/`   | `main`        | Urgent production fix          | `main`      |
| `releases/` | `main`        | Version cut, e.g. `releases/4.0.0` |   |

`develop` is the integration branch for ongoing work; `main` tracks released versions.

**Release flow**: all `feature/*`/`fix/*` branches merge into `develop`. When it's time to cut a
release, open a PR merging `develop` into `main` describing the features/fixes included. A
`releases/<semver>` branch is then cut from `main` at that point using
[Semantic Versioning](https://semver.org/) — e.g. `releases/4.0.0`. New releases start as
pre-releases with a suffix (`-beta1`, `-rc1`, ...) before the final tag is cut.

## Code style

Governed by `src/.editorconfig` — treat it as authoritative, don't fight its rules (e.g. explicit
types only, no `var`; file-scoped namespaces; braces required). For anything not covered there,
follow Microsoft's naming/design guidelines:

- [C# identifier naming conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names)
- [.NET general naming conventions](https://learn.microsoft.com/dotnet/standard/design-guidelines/naming-guidelines)
- [Razor / `.cshtml` syntax reference](https://learn.microsoft.com/aspnet/core/mvc/views/razor)

Quick recap:

| Element                         | Convention     | Example         |
| -------------------------------- | -------------- | --------------- |
| Classes / Methods / Properties   | `PascalCase`   | `GetOrderAsync` |
| Private fields                   | `_camelCase`   | `_repository`   |
| Parameters / locals              | `camelCase`    | `orderId`       |
| Async methods                    | `Async` suffix | `GetOrderAsync` |
| Interfaces                       | `I` + PascalCase | `ILockManager` |

## Assumption comments

When a business rule or intended behavior isn't specified by the user or discoverable from
existing code/docs, don't silently guess:

- Mark it inline: `// ASSUMPTION: <what you assumed>. Adjust if the actual rule differs.`
- Call it out again in your final summary/PR description so the user can confirm or correct it.

## General principles

- Prefer dependency injection and interfaces for LiteDB repositories, Hangfire jobs, and crawler
  agents so they stay unit-testable in isolation.
- Don't introduce new persistence mechanisms — LiteDB for domain data, SQLite only for Hangfire —
  unless explicitly requested otherwise.
