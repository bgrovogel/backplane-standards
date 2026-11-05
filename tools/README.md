# tools

Small scripts that check this repository's own documents. They need only Python 3, and each one exits non-zero when it finds a problem, so it can be a gate.

| Script           | What it checks                                                                         |
| ---------------- | -------------------------------------------------------------------------------------- |
| `check_links.py` | Every relative link and heading anchor in the Markdown points at something that exists |

**What does not live here:** application code, and the checks that belong to an application (`mortgage-analytics` keeps its test-count and citation checks in its own `tools/doc-check`; they are lifted into
this repository by roadmap task 1.9). **Who may depend on it:** nothing; these are run by hand and by the check scripts. **Read more:** [Documentation standard](../docs/10-documentation-standard.md#keeping-documentation-true).
