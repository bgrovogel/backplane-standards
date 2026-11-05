# gitleaks

> A command line tool that looks through a git repository (its files, its staged changes, its whole history) for things that look like secrets: passwords, API keys, tokens. We run it before every commit so a secret never reaches the history.

**Read time:** 9 min · **Version:** gitleaks v8.30.1 (the latest release on GitHub, 2026-03-21) · **Status:** a step in `scripts/check.ps1` (`fast` and `release` tiers) that is skipped, loudly, where gitleaks is not installed; not yet installed on every machine, and not yet in CI · **Last verified:** 2026-11-05 against the project README and its `git` command source

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) · [How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) · [What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

A **secret** is a value that proves who you are to another system: a database password, a cloud access key, a signing key, a payment-provider token. Anyone who has the value can act as you. **gitleaks** is an open-source scanner that finds them. The README describes it as "a tool for detecting secrets like passwords, API keys, and tokens in git repos, files, and whatever else you wanna throw at it via `stdin`".

It has three scanning modes: `git` (a repository and its history), `dir` (a folder or file) and `stdin` (text piped in).

## Why we use it

Git never forgets. A secret committed once is in the history, in every clone, and in every fork, even after the line is deleted in the next commit. The only real cure is to **change the secret** (rotate it), which is slow and sometimes costly. Catching it **before the commit** costs a few seconds. Our [security baseline](../07-security-baseline.md) says a secret scan runs before every commit (gate G-13 in [document 3](../03-quality-gates.md#the-gates)) and weekly across the history.

Alternatives: relying on review (people miss a 40-character string in a 600-line diff); a scan only in CI (the secret is already pushed, and the history already has it); a hosted scanner (a service, and a repository that is private should not need to share its content). A local scan is the cheapest place to stop it.

The project says of itself that it is "feature complete" and in maintenance: future releases are security patches only. That makes it stable to depend on, and means we should not expect new features.

## The big idea

> **Look at the text that is about to become history, match it against patterns of known secrets, and stop with a non-zero exit code if anything matches.**

## How it works inside

1. **Choose the text.** In `git` mode gitleaks runs `git log -p` and scans each patch (the lines that were added), so it covers history; `--log-opts` passes options to `git log` (for example a range of commits). With `--staged` it scans only the changes staged for the next commit, "good for pre-commit" in its own flag description. With no target it uses the current directory.
2. **Apply the rules.** Each rule in the configuration has a regular expression for the secret's shape, optional `keywords` (cheap words that must be present before the regex is tried, "pre-regex check filtering"), an optional `entropy` (a minimum measure of randomness, because `password` in a comment is not a password but `x8Kq9...` often is), and tags. The default rule set ships inside the program.
3. **Filter the findings.** An **allowlist** removes findings by commit, path, regular expression or stop word. A rule can have its own allowlists and the configuration can have global ones; a global allowlist takes precedence. A line carrying a `gitleaks:allow` comment is skipped. A `.gitleaksignore` file lists the **fingerprints** of findings to ignore (a fingerprint is commit:file:rule:line); the README marks it experimental.
4. **Report.** Findings print to the terminal (`--redact` hides the secret itself in the output) and can be written with `--report-path` as json, csv, junit, sarif or a template. The exit code is 0 when nothing is found and 1 when a leak is found or an error happens (`--exit-code` changes it). That code is what makes it a gate.
5. **Configuration.** Chosen in this order: `--config`, the environment variable `GITLEAKS_CONFIG`, the variable `GITLEAKS_CONFIG_TOML` (the file's content), then `.gitleaks.toml` in the target path; otherwise the built-in default. A file can add to the defaults with `[extend] useDefault = true`.
6. **Baseline.** `--baseline-path` takes an earlier report and ignores every finding already in it, so a repository with an old history can start with a clean slate for _new_ secrets while the old ones are rotated.

```
   git diff --staged  ->  rules (keywords -> regex -> entropy)  ->  allowlists  ->  findings
                                                                       exit 0 (clean) or 1 (leak)
```

## How it fits with everything else

- `scripts/check.ps1` runs `gitleaks git --staged` in the `fast` tier (before every commit) and `gitleaks git` over the whole history in the `release` tier. Where gitleaks is not installed, the step is reported as SKIPPED with how to install it, never as passed ([quality gates](../03-quality-gates.md#one-command-per-tier)).
- It is a different tool from a dependency audit: gitleaks looks for secrets _we_ wrote; [`tools/dependency_report.py`](../../tools/README.md) looks for known-vulnerable packages we _use_.
- A secret that is found is **rotated first**, then removed. Removing it from the code does not undo the exposure.

## An example from our code

`scripts/check.ps1`, two of its steps:

```powershell
@{ Tier = 'fast'; Name = 'Secret scan, staged changes'; Dir = '.'; Cmd = 'gitleaks git --staged --redact --no-banner'; Needs = 'gitleaks' },
@{ Tier = 'release'; Name = 'Secret scan, whole history'; Dir = '.'; Cmd = 'gitleaks git --redact --no-banner'; Needs = 'gitleaks' }
```

- `git` is the mode introduced in v8.19.0, which deprecated the older `detect` and `protect` commands (still available, hidden from the help). `git --staged` does the job the older `protect` command did.
- `--redact` keeps the secret out of the terminal and out of any log a CI run keeps; `--no-banner` removes the logo.
- `Needs = 'gitleaks'` is what makes the script say SKIPPED, loudly, when the program is missing.

## How to use it

```text
winget install gitleaks                 # or: scoop install gitleaks   (the README also lists Homebrew, Docker, Go)
gitleaks git --staged --redact          # what is about to be committed
gitleaks git --redact                   # the whole history
gitleaks dir . --redact                 # the working files, tracked or not
```

To run it as a git hook without our script, the README shows a `pre-commit` configuration (a `.pre-commit-config.yaml` naming the gitleaks repository and the `gitleaks` hook, then `pre-commit install`); `SKIP=gitleaks git commit` skips it once.

When it finds something, in this order: **(1)** is it real? If yes, rotate the secret now, then remove it from the code and read it from configuration or a secret store instead. **(2)** Is it a harmless test value? Put `gitleaks:allow` on that line, or add a narrow allowlist (one path or one regular expression) to `.gitleaks.toml` with a comment saying why.

## The rules we follow

| Rule                                                                | Enforced by                                     |
| ------------------------------------------------------------------- | ----------------------------------------------- |
| A scan of the staged changes runs before every commit               | `check fast`; the commit routine                |
| A scan of the whole history runs before a release, and weekly       | `check release`                                 |
| A found secret is rotated before it is removed                      | [Security baseline](../07-security-baseline.md) |
| An allowlist entry is as narrow as it can be and says why           | Review                                          |
| `--redact` is always on, so a log never holds the secret it reports | The script's command lines                      |
| A skipped scan is a scan not done, and is reported as such          | The script's summary                            |

## What goes wrong

| Symptom                                                    | Cause                                                             | Fix                                                                                |
| ---------------------------------------------------------- | ----------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| The scan says SKIPPED                                      | gitleaks is not installed on this machine                         | Install it (see the note in the summary); the check was not done until you do      |
| A test fixture or sample key is flagged                    | It looks like a real key because it is shaped like one            | `gitleaks:allow` on the line, or a narrow path allowlist with a reason             |
| A real secret is in an old commit                          | It was committed before the scan existed, or the scan was skipped | Rotate it. Rewriting history is a last resort and does not un-leak a pushed secret |
| The whole-history scan reports hundreds of old items       | A long history with old keys, mostly rotated                      | Create a baseline from a report and gate on new findings only                      |
| `--staged` finds nothing though a secret is in a file      | The file is not staged (or the secret is in an untracked file)    | `git add` it first, or use `gitleaks dir .` for the working files                  |
| Commands from older tutorials (`detect`, `protect`) differ | Version 8.19.0 replaced them with `git`, `dir` and `stdin`        | Use the new commands; the old ones still work but are hidden                       |

## Terms

| Term            | Meaning                                                                                |
| --------------- | -------------------------------------------------------------------------------------- |
| Secret          | A value that proves identity to another system (password, key, token)                  |
| Rotate          | Replace a secret with a new one and revoke the old, so a leaked copy stops working     |
| Entropy         | A measure of how random a string looks; real keys are random, ordinary words are not   |
| Allowlist       | A rule that says "do not report this" by commit, path, regex or word                   |
| Baseline        | An earlier report whose findings are ignored on later runs                             |
| Fingerprint     | An identifier of one finding, used in `.gitleaksignore`                                |
| Pre-commit hook | A program git runs before it records a commit; if it fails, the commit does not happen |

## Further reading

- [gitleaks on GitHub](https://github.com/gitleaks/gitleaks): the README covers installation, the commands, the configuration format and the allow comments; [the `git` command's source](https://github.com/gitleaks/gitleaks/blob/master/cmd/git.go) lists its flags (`--staged`, `--pre-commit`, `--log-opts`).
- [Releases](https://github.com/gitleaks/gitleaks/releases) for the version and the change that introduced `git`, `dir` and `stdin` (8.19.0).
- Our [security baseline](../07-security-baseline.md) for what to do after a leak.
