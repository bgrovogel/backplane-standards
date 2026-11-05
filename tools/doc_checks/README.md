# tools/doc_checks

The documentation checks, lifted from `mortgage-analytics` and made to work for any repository: links and citations (`refs`), the generated `MAP.md` (`map`), a primer for every package (`primers`), and test counts that match the real suites (`counts`). Each is a Python module with tests that show it failing on a sample that breaks it; `run.py` runs them as a gate.

```text
python tools/doc_checks/run.py                    # refs, map and primers on this repository
python tools/doc_checks/run.py map                # rewrite MAP.md
python tools/doc_checks/run.py counts             # slow: lists every test suite
python tools/doc_checks/run.py --root ../app --config ../platform/tools/doc_checks/examples/mortgage-analytics.json
```

**What each looks at is the repository's `doc-checks.json`** (this repository's is at the root): `refs` (`cited_guides`, `citations`, `stay_inside`), `map` (`named_folders`, `groups`, `command`), `primers` (`index`, `dependency_files`, `covers`, `exempt` with a reason for each, and `template`: every primer has the header, the sections in order and a linked source), `counts` (`sources` to run and read, `sums`, `rules` for where the documents state a count). With no file, every check has a default and a check with nothing to look at passes.

**What does not live here:** the checks on code (those are [`Backplane.Conventions`](../../src/Backplane.Conventions/README.md)) and the work board (`tools/board.py`). **Who may depend on it:** nothing; it is run by hand, by CI and by the check scripts. **Read more:** [Documentation standard](../../docs/10-documentation-standard.md#keeping-documentation-true); [`examples/mortgage-analytics.json`](examples/mortgage-analytics.json) is the application these were lifted from, and the acceptance test runs them on it.
