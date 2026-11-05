# tools

Small scripts that check this repository's own documents. They need only Python 3, and each one exits non-zero when it finds a problem, so it can be a gate.

| Script                 | What it checks                                                                                                                                                                                  |
| ---------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `doc_checks/`          | The documentation checks: links and citations, `MAP.md`, a primer for every package and each primer in the template, test counts. `python tools/doc_checks/run.py`                              |
| `dependency_report.py` | Vulnerable packages (NuGet, including the ones another package brings in, and npm) fail; newer versions are listed. Part of `check release`                                                     |
| `test_check_script.py` | `scripts/check.ps1` still names real npm scripts and paths, each tier keeps its gates, and everything CI runs is in the script                                                                  |
| `mutation.py`          | Runs Stryker for a chosen project from the shared base in `mutation.json`, scores the report and records it in [`docs/mutation-scores.md`](../docs/mutation-scores.md). Part of `check release` |
| `test_mutation.py`     | How a configuration is made, how a report becomes a score, and that a failed run is not scored by the run before it                                                                             |
| `board.py`             | Reads the work board ([`docs/work-board.md`](../docs/work-board.md)): `next`, `next --unattended`, `check`, `list`                                                                              |
| `test_board.py`        | The board's rules, each shown on a small board that breaks it, and the real board held to them                                                                                                  |

**What does not live here:** application code, and the checks that belong to an application (`mortgage-analytics` still has its own copies in `tools/doc-check`, which it drops when it takes these). **Who may depend on it:** nothing; these are run by hand and by the check scripts. **Read more:** [Documentation standard](../docs/10-documentation-standard.md#keeping-documentation-true).
