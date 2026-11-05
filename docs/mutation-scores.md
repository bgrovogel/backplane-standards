# Mutation scores

The mutation score of each project that is mutation-tested: the percent of the mutants (small deliberate bugs) that its tests caught ([Stryker primer](primers/stryker.md), gate G-22 in [document 3](03-quality-gates.md)). One row per project, the latest measurement, written by `python tools/mutation.py run <project> --record` from a clean working tree. **Break** is the score below which the run fails.

| Project              | Score | Killed | Survived | Break | Measured   | At      |
| -------------------- | ----- | ------ | -------- | ----- | ---------- | ------- |
| Platform.Conventions | 99.5  | 385    | 2        | 95    | 2026-11-05 | 9149a79 |

## Survivors accepted

A survivor is either a missing test or a mutant that cannot change behaviour. `Platform.Conventions` has five that cannot, left unmarked because Stryker's `once` comment would switch off every mutant of the same kind in the whole member:

- `LayerRule.UsingProblems`: `directive.Name?.ToString() ?? string.Empty`. A `using` directive always has a name; the fallback only satisfies the nullable annotations.
- `NamingRule`: `name.Length is > 0 and < MinimumLength` becoming `>= 0`. An empty name has no letter other than `_`, so the next condition already refuses it.

- `ConventionFixture`: the end-to-end set reads `.ts` files; the `".ts"` is exercised only by the acceptance run on another application, which needs a checkout of it.
- `TextSet.OfFolders`: `EnumerateFiles(folder, "*", ...)` with the pattern `""` returns the same files, so the pattern mutant changes nothing.
- `PlaywrightExpectRule`: `close < 0` becoming `close <= 0`. A closing bracket can never be at index 0, because the opening one is before it.

Three more are marked in the source with `// Stryker disable once ... : reason` (`ComplexityRule`, `SuppressionRule`, `LoggingRule`), each at one line.
