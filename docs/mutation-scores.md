# Mutation scores

The mutation score of each project that is mutation-tested: the percent of the mutants (small deliberate bugs) that its tests caught ([Stryker primer](primers/stryker.md), gate G-22 in [document 3](03-quality-gates.md)). One row per project, the latest measurement, written by `python tools/mutation.py run <project> --record` from a clean working tree. **Break** is the score below which the run fails.

| Project              | Score | Killed | Survived | Break | Measured   | At      |
| -------------------- | ----- | ------ | -------- | ----- | ---------- | ------- |
| Platform.Conventions | 56.4  | 221    | 171      | 55    | 2026-11-05 | fdf977f |

## Survivors accepted

A survivor is either a missing test or a mutant that cannot change behaviour. `Platform.Conventions` has two that cannot, left unmarked because Stryker's `once` comment would switch off every mutant of the same kind in the whole member:

- `LayerRule.UsingProblems`: `directive.Name?.ToString() ?? string.Empty`. A `using` directive always has a name; the fallback only satisfies the nullable annotations.
- `NamingRule`: `name.Length is > 0 and < MinimumLength` becoming `>= 0`. An empty name has no letter other than `_`, so the next condition already refuses it.

Three more are marked in the source with `// Stryker disable once ... : reason` (`ComplexityRule`, `SuppressionRule`, `LoggingRule`), each at one line.
