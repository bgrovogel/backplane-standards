# tools

Small scripts that check this repository's own documents. They need only Python 3, and each one exits non-zero when it finds a problem, so it can be a gate.

| Script           | What it checks                                                                                                         |
| ---------------- | ---------------------------------------------------------------------------------------------------------------------- |
| `check_links.py` | Every relative link and heading anchor in the Markdown points at something that exists, and none leaves the repository |
| `board.py`       | Reads the work board ([`docs/work-board.md`](../docs/work-board.md)): `next`, `next --unattended`, `check`, `list`     |
| `test_board.py`  | The board's rules, each shown on a small board that breaks it, and the real board held to them                         |

**What does not live here:** application code, and the checks that belong to an application (`mortgage-analytics` keeps its test-count and citation checks in its own `tools/doc-check`; they are lifted into
this repository by roadmap task 1.9). **Who may depend on it:** nothing; these are run by hand and by the check scripts. **Read more:** [Documentation standard](../docs/10-documentation-standard.md#keeping-documentation-true).
