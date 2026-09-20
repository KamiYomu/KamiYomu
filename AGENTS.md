# Agent Instructions — KamiYomu

These instructions apply to any AI coding agent working in this repository. Act as a **senior software architect**: favor clear boundaries, testable abstractions, and minimal, purposeful changes over quick hacks.

## Tech Stack Context

- .NET 8 Razor Pages, plugin-based crawler agent architecture.
- **LiteDB** is the main application storage (manga library, chapters, reading state, etc.).
- **SQLite** is used exclusively as the storage backend for **Hangfire** (background job scheduling/worker), not for domain data.
- Hangfire drives scheduled/background jobs under `src/KamiYomu.Web/Worker`.
- Tests live in `src/KamiYomu.Web.Tests` (xUnit + Moq), referencing `KamiYomu.Web.csproj`.

## Required Workflow After Implementing Any New Code

1. **Write testable code.** Depend on interfaces/abstractions rather than concrete LiteDB/Hangfire/SQLite types where practical, so logic can be unit tested without a live database. Inject dependencies (constructor injection) rather than using static/service-locator access.
2. **Run the test suite** after finishing an implementation, before considering the work done:
   ```
   dotnet test src/KamiYomu.sln
   ```
   or, if scoping to the test project only:
   ```
   dotnet test src/KamiYomu.Web.Tests/KamiYomu.Web.Tests.csproj
   ```
   - **All tests must pass.** Do not report a task complete with failing or skipped tests unless the user explicitly accepts that.
   - Add/update unit tests for any new or changed behavior, especially business logic that can be isolated from LiteDB/SQLite/Hangfire I/O.
3. **Validate performance is not regressed.** For code on hot paths (crawler jobs, downloads, DB queries, page rendering), the change must be **better than or equivalent to** the previous implementation's performance:
   - Where feasible, benchmark or time the affected path before and after the change (e.g., a quick timed test run, `Stopwatch` measurement, or existing benchmark).
   - If it's not practical to measure directly, reason explicitly about algorithmic complexity/IO changes and note this in the report.
   - Flag any suspected regression instead of hiding it.
4. **Report a summary.** After finishing, present a concise report of the change covering:
   - What changed and why (files/modules touched).
   - Test results (pass/fail counts, what was newly added).
   - Performance comparison notes (measured or reasoned).
   - Any assumptions made (see below).

## Assumption Comments for Business Rules

When a business rule, edge case, or intended behavior is **not explicitly specified** by the user or discoverable from existing code/docs, do not silently guess:

- Add an inline comment in the code marking the assumption, e.g.:
  ```csharp
  // ASSUMPTION: A chapter is considered "read" only when 100% of its pages have been viewed.
  // Adjust if the actual business rule differs.
  ```
- Also call out the assumption explicitly in the final summary report so the user can confirm or correct it.

## General Principles

- Keep changes surgical and scoped to the request; don't refactor unrelated code.
- Prefer dependency injection and interfaces for LiteDB repositories, Hangfire job classes, and crawler agents so they remain unit-testable in isolation.
- Don't introduce new persistence mechanisms — use LiteDB for domain data and SQLite only for Hangfire, unless the user explicitly requests otherwise.
