# Mutation scores

The mutation score of each project that is mutation-tested: the percent of the mutants (small deliberate bugs) that its tests caught ([Stryker primer](primers/stryker.md), gate G-22 in [document 3](03-quality-gates.md)). One row per project, the latest measurement, written by `python tools/mutation.py run <project> --record` from a clean working tree. **Break** is the score below which the run fails.

| Project              | Score | Killed | Survived | Break | Measured   | At      |
| -------------------- | ----- | ------ | -------- | ----- | ---------- | ------- |
| Platform.Conventions | 56.4  | 221    | 171      | 55    | 2026-11-05 | fdf977f |
