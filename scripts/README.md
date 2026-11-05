# scripts

Scripts you run on your own machine: today, `check.ps1`, which runs this repository's checks one command per tier. It works in Windows PowerShell 5.1 and in `pwsh`.

- **Lives here:** `check.ps1` with its three tiers, `fast` (before every commit), `full` (everything CI runs) and `release` (adds mutation testing, the dependency report and the whole-history secret scan). It reports each step as passed, failed or skipped, and a step whose tool is missing says so loudly instead of passing.
- **Does not live here:** the Python tools it calls (`tools/`), or CI itself (`.github/workflows/ci.yml`, which keeps its own jobs). Nothing in here adds a CI step or uses CI minutes.
- **Who may depend on it:** nobody in the code. `tools/test_check_script.py` reads `check.ps1` and fails when a step names an npm script or a path that does not exist, or when CI runs a gate the script does not.
- **Read more:** [Quality gates](../docs/03-quality-gates.md) and [Ways of working](../docs/14-ways-of-working.md).
