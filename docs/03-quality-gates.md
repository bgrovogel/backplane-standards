# 3. Quality gates

> The strict rules. A change is not finished until every gate passes, and a gate is a machine that says no, not a
> reminder.

**Read time:** 20 min · **Best for:** knowing exactly what must pass, where, and with what numbers · **Builds on:**
[Principles](01-principles.md), [Architecture](02-architecture.md) · **Next:** [Testing strategy](04-testing-strategy.md)

**On this page:** [How gates work](#how-gates-work) · [Definition of done](#definition-of-done) · [The four tiers](#the-four-tiers) ·
[The gates](#the-gates) · [Rules that apply to every gate](#rules-that-apply-to-every-gate) · [Changing a gate](#changing-a-gate) ·
[Commits and reviews](#commits-and-reviews) · [Release and deploy](#release-and-deploy) · [One command per tier](#one-command-per-tier)

---

## How gates work

A **gate** is an automatic check that fails the build or blocks a merge. Each gate has:

- an **ID** (so conversations and exceptions can name it),
- a **tool** that performs it,
- a **threshold** or rule (a number, or "zero violations"),
- a **tier**: when it runs (see below),
- a **status**: whether it **exists** today (and where), or is **new** to the platform.

Status is stated honestly. Numbers marked "from `mortgage-analytics`" were read from that repository on 2026-11-03 and are the
starting point; they may be raised, and lowered only through [the exception process](#changing-a-gate).

---

## Definition of done

A piece of work is **done** only when **all** of these are true. This is the checklist every change is reviewed against.

1. **A failing test came first.** The behaviour is specified by a test that was red before the code existed ([TDD](01-principles.md#tdd)).
2. **Every gate in tiers 1 and 2 passes locally**, then on CI.
3. **Input is validated at the boundary**, with a validator test for every rule ([document 5](05-validation-and-errors.md)).
4. **It is observable:** the new path logs what is needed to diagnose it, with no personal data ([document 6](06-logging-observability.md)).
5. **It meets the security baseline:** authorization on every endpoint, tenant from the token, no secret in code ([document 7](07-security-baseline.md)).
6. **A side effect is proved by a test that asserts the call was made with the right arguments**, not merely that a status field
   changed. (Hyvva's ISSUE-166 and the-hive's "done means more than green tests" both came from a feature that looked finished
   and never actually sent the email or charged the card.)
7. **A feature is reachable.** A backend feature with no route, button or link in the UI is "in progress", not done. If the work
   is backend-only, say so and record what is missing.
8. **UI work meets accessibility:** keyboard use, screen-reader names, contrast, text resizing, reduced motion, touch targets, and an
   axe check on the page ([document 4](04-testing-strategy.md#accessibility-axe-and-manual)).
9. **UI work has loading, empty and error states**, and every user-visible string is translatable with every language complete.
10. **Documentation is current:** counts and facts in the docs match (the docs check), a new tool has its
    [primer](10-documentation-standard.md), a hard-to-reverse choice has an [ADR](adr/README.md), and a changed behaviour is in the
    changelog.
11. **No placeholder or mock content is reachable** in a real user flow.
12. **Nothing was disabled to get here:** no skipped test, no relaxed threshold, no silenced warning without a written reason.

---

## The four tiers

Gates run at different times so that feedback is fast and the CI-minutes budget is respected
([document 9](09-repositories-and-delivery.md#the-ci-minutes-budget)).

| Tier  | When                             | Where                     | Budget                       | Purpose                                                                        |
| ----- | -------------------------------- | ------------------------- | ---------------------------- | ------------------------------------------------------------------------------ |
| **1** | Before every commit              | Your machine              | under 1 minute               | Format, lint, typecheck, fast unit tests: no round trips with CI for typos     |
| **2** | Every push to a branch or `main` | CI                        | about 10 minutes             | Everything that must be true of every change                                   |
| **3** | Nightly or weekly                | CI schedule               | no budget pressure on pushes | Slow or drifting checks: dependency advisories, Lighthouse, full mutation      |
| **4** | Before a release                 | Your machine, one command | as long as needed            | Expensive checks: cross-browser, load, independent models, real-database tests |

A check lives in the **cheapest tier that still catches the problem in time.** Slow checks never sit in tier 2 just because that is
where checks usually go.

---

## The gates

### Build and static analysis

| ID   | Gate                         | Tool                                                                        | Rule                                                                                                                                                                                                                                                                          | Tier | Status                                                                                                                                                       |
| ---- | ---------------------------- | --------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| G-01 | Compiler strictness          | MSBuild                                                                     | `TreatWarningsAsErrors`, `Nullable` enabled, `AnalysisLevel` `latest-recommended`                                                                                                                                                                                             | 1, 2 | Exists (`Directory.Build.props`)                                                                                                                             |
| G-02 | Code style in the build      | `EnforceCodeStyleInBuild` + `.editorconfig`                                 | Formatting (IDE0055), unused usings (IDE0005), braces (IDE0011), `var` rules                                                                                                                                                                                                  | 1, 2 | Exists                                                                                                                                                       |
| G-03 | Pinned dependencies          | NuGet lock files, `npm ci`                                                  | Restore in locked mode; `packages.lock.json` and `package-lock.json` committed                                                                                                                                                                                                | 2    | Exists                                                                                                                                                       |
| G-04 | Public API documented        | Compiler (CS1591)                                                           | Every public member has an XML summary (production projects)                                                                                                                                                                                                                  | 1, 2 | Exists                                                                                                                                                       |
| G-05 | Formatting everywhere        | Prettier, `dotnet format`, `ruff format`                                    | Zero differences across code, docs, YAML, JSON, props and shell files                                                                                                                                                                                                         | 1, 2 | Exists                                                                                                                                                       |
| G-06 | Lint                         | ESLint (`curly`, `id-length`, Angular a11y template rules), stylelint, ruff | Zero errors                                                                                                                                                                                                                                                                   | 1, 2 | Exists                                                                                                                                                       |
| G-07 | Strict typecheck incl. tests | `tsc` for specs and e2e                                                     | Zero errors (Jest does not type-check specs; CI once caught what Jest passed)                                                                                                                                                                                                 | 1, 2 | Exists                                                                                                                                                       |
| G-08 | Conventions                  | Roslyn conventions tests                                                    | Assertion messages, `Assert.Multiple`, Playwright `expect` messages, one type per file, no nested classes, names of 3+ letters, `var` only where the type shows                                                                                                               | 2    | **Packaged**: 15 rules in `Backplane.Conventions`                                                                                                            |
| G-09 | Architecture and ratchets    | NetArchTest and scan tests                                                  | Layer references, contracts never expose domain types, no `DateTime.Now`, no `dynamic`, no `console.log`, no `any`, no float money                                                                                                                                            | 2    | **Partly packaged**: `LayerRule` (with the AWS SDK ban), `LocalTimeRule`, `DynamicRule` and the float and method-size ratchets; contract layering is task 24 |
| G-10 | Import boundaries (frontend) | ESLint import rule                                                          | Features never import features; components never use `HttpClient`                                                                                                                                                                                                             | 1, 2 | **Part built**: the layer rule is in `frontend-presets`; the `HttpClient` rule is task 26                                                                    |
| G-11 | Structure and names          | Conventions test                                                            | A README in every project and top-level folder; no `Utils`, `Helpers`, `Common`, `Manager` (`VagueNamesRule`)                                                                                                                                                                 | 2    | **Packaged** (`VagueNamesRule`, `ReadmeRule`)                                                                                                                |
| G-12 | Complexity                   | Analyzer rules                                                              | Method length **50 lines** and cyclomatic complexity **12** for every method and constructor. Code already past them is listed in a ratchet with its size today, so it can only shrink; a ceiling that has become slack fails too (measured on the reference app, 2026-11-03) | 1, 2 | **Packaged** (`ComplexityRule`); the ceilings are each application's                                                                                         |
| G-13 | Secret scan                  | gitleaks, run locally                                                       | Zero findings in the working tree and staged changes                                                                                                                                                                                                                          | 1    | **Packaged** in `scripts/check.ps1` (local; no CI minutes)                                                                                                   |
| G-14 | Docs match reality           | `doc_counts.py`-style check                                                 | Test counts, versions, file and line citations and links in the docs are correct                                                                                                                                                                                              | 2    | **Packaged** in `tools/doc_checks` (counts, links, citations, README per folder, primer per package, map)                                                    |
| G-15 | Sentences have codes         | Roslyn conventions test                                                     | A validator never says a sentence of its own with `WithMessage`: it uses `Reject(code, template, values)`, with a code written `area.what_went_wrong`, a template that is a plain string, and a value for every `{name}`                                                      | 1, 2 | **Exists** in `mortgage-analytics`; not packaged (needs the kit `Reject` helper, task 20)                                                                    |
| G-16 | Translations complete        | Jest spec                                                                   | Every message a person can read has its translation, and every code the API can refuse with has its sentence, with the same numbers and formats; a stale translation fails too                                                                                                | 1, 2 | **Exists** in `mortgage-analytics`; not packaged (task 25)                                                                                                   |
| G-17 | One source for each limit    | Approved limits file + Jest spec                                            | A number a form repeats equals the limit read from the API's own validators; a limit changed on one side alone fails; a reason a form may differ is written beside the field                                                                                                  | 1, 2 | **Exists** in `mortgage-analytics`; not packaged (task 25)                                                                                                   |

### Tests

| ID   | Gate                        | Tool                                 | Rule                                                                                                                                                                          | Tier | Status                                                                  |
| ---- | --------------------------- | ------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---- | ----------------------------------------------------------------------- |
| G-20 | Unit and integration tests  | NUnit, Jest                          | All pass, none skipped without a linked reason                                                                                                                                | 2    | Exists                                                                  |
| G-21 | Coverage                    | coverlet, Jest                       | Backend line **and** branch at least **85%**; frontend statements, branches, functions and lines at least **85%**                                                             | 2    | Exists (85/85)                                                          |
| G-22 | Mutation score, logic       | Stryker.NET                          | Domain: **break at 95**, high 97. Application: high 95, low 90, break proposed at 90 once measured. Frontend logic (Stryker with Jest): high 95, low 90, break proposed at 90 | 3, 4 | Domain exists in CI; the rest run locally                               |
| G-23 | API contract                | Approved OpenAPI snapshot            | The generated OpenAPI document equals the approved file; changing it needs a deliberate re-approval                                                                           | 2    | Exists (`ContractSnapshotTests`); not packaged (task 27)                |
| G-24 | Migrations                  | EF Core test + Testcontainers        | Model matches migrations; migration applies to an empty database and upgrades from the previous release                                                                       | 2, 4 | Exists (model check); upgrade test exists locally                       |
| G-25 | End-to-end                  | Playwright (Chromium)                | Critical journeys pass on the production build                                                                                                                                | 2    | Exists                                                                  |
| G-26 | Accessibility               | axe-core through Playwright          | Zero violations on every page, in every theme and language                                                                                                                    | 2    | Exists (54 axe checks at the 28 Oct 2026 count); not packaged (task 28) |
| G-27 | Reader-loss checks          | Playwright                           | Nothing is lost at 2x text size, WCAG text spacing, 320 px width, forced colours, keyboard-only                                                                               | 2    | Exists; not packaged (task 28)                                          |
| G-28 | Layout stability            | Playwright + layout-shift observer   | Cumulative layout shift per page under its budget (the app holds 0.1)                                                                                                         | 2    | Exists; not packaged (task 28)                                          |
| G-29 | Cross-browser               | Playwright Firefox and WebKit        | The journeys pass                                                                                                                                                             | 3, 4 | Exists (manual script)                                                  |
| G-30 | Size budget                 | Angular build budget                 | Initial bundle warning 870 kB, error 900 kB (the app's numbers; each app sets its own)                                                                                        | 2    | Exists                                                                  |
| G-31 | Performance budget          | Lighthouse CI                        | LCP, CLS and TBT within budget on the production build                                                                                                                        | 3    | Exists in Hyvva (nightly)                                               |
| G-32 | Load check                  | `tools/load-check` style script      | Throughput and latency not below the recorded baseline; limits return 429 or 503, never a crash                                                                               | 4    | Exists in `mortgage-analytics`                                          |
| G-33 | Independent oracle          | A second implementation of the maths | Hundreds of random inputs agree with the main implementation to the cent                                                                                                      | 4    | Exists in `mortgage-analytics` (Python)                                 |
| G-34 | Tenant isolation            | Integration tests                    | A user of tenant A can never read or change tenant B's data, including through every list, search and export                                                                  | 2    | Exists in Hyvva                                                         |
| G-35 | Infrastructure assertions   | CDK assertions                       | Each stack has the resources, encryption, retention and permissions it must                                                                                                   | 2    | Exists in Hyvva and the-hive                                            |
| G-36 | Infrastructure policy       | cdk-nag, IAM lint                    | No wildcard IAM actions, no public buckets, encryption on, MFA where required                                                                                                 | 2    | Exists in the-hive; ratchet in Hyvva                                    |
| G-37 | Container or package builds | The real build                       | The deployable artifact builds and starts (a smoke call to `/health`)                                                                                                         | 2    | Exists (Docker job)                                                     |

### Supply chain and operations

| ID   | Gate                    | Tool                                                                 | Rule                                                                            | Tier   | Status                            |
| ---- | ----------------------- | -------------------------------------------------------------------- | ------------------------------------------------------------------------------- | ------ | --------------------------------- |
| G-40 | Vulnerable dependencies | `dotnet list package --vulnerable --include-transitive`, `npm audit` | No high or critical advisory; run when a manifest changes and weekly            | 3      | Exists in Hyvva and the-hive      |
| G-41 | Dependency freshness    | Renovate-style report, run locally                                   | Report of outdated packages, reviewed monthly; major upgrades go through an ADR | 4      | **New**                           |
| G-42 | Post-deploy smoke       | A canary or script                                                   | The key request succeeds in the real environment after every deploy             | deploy | Exists in the-hive (canaries)     |
| G-43 | Alarms exist            | CDK assertion                                                        | Error-rate, latency and queue-age alarms exist for each deployed capability     | 2      | Exists in the-hive (`slo-alarms`) |

---

## Rules that apply to every gate

1. **No gate is skipped.** No `--no-verify`, no `[skip ci]` on a change that touches code, tests, configuration or dependencies.
   `[skip ci]` is for documentation-only commits.
2. **A suppression carries its reason.** `#pragma warning disable`, `[SuppressMessage]`, `// eslint-disable` and `pytest.skip`
   must include a one-line reason and, if broad, a link to an ADR. A conventions test rejects a suppression without one (**new**).
3. **A failing test is never "retried until green."** There are no automatic retries (`mortgage-analytics` sets Playwright `retries: 0` on purpose: "a flaky
   test should fail loudly and be fixed, not be hidden by a second attempt"). A test that fails once without an explanation is investigated within a
   day: open the trace, find the cause. In `mortgage-analytics`, a "flaky" password-change test was a real race found from the trace. Quarantining
   (marking a test known-flaky) needs an issue and an owner.
4. **Adopt new rules with a ratchet.** A new rule on existing code starts as "at most N violations" and tightens to zero; it never
   starts disabled.
5. **A bug fix starts with a test that reproduces the bug.** The test is red, then the fix makes it green, and the test stays.
6. **Thresholds only go up.** Coverage, mutation score and budgets are raised when the code allows; lowering one needs the
   exception process.
7. **Generated and tool-written files are excluded explicitly**, in one place per tool, with a comment saying why (migrations,
   the OpenAPI snapshot, lock files). Nothing is excluded by silence.

---

## Changing a gate

A gate may be loosened, removed or given an exception only like this:

1. Open an [ADR](adr/README.md) that names the gate ID, the change, the reason, and the date it will be revisited.
2. The owner approves it in the same change.
3. The exception is as narrow as possible (one file, one rule, one project) and lists its expiry.
4. The gate's row in this document is updated in the same change.

Tightening a gate, or adding a new one, needs no ADR: do it, and update this page.

---

## Commits and reviews

- **One idea per commit.** A commit message says _what changed and why_ in its first lines; the body explains anything not obvious
  from the diff. Style: `type(scope): summary` (for example `feat(email): retry with backoff`).
- **Small changes.** A review should fit in one sitting; a large change is split.
- **Self-review the staged diff** before committing: `git diff --staged`. Stage named paths, not `git add -A`.
- **Never force-push `main`.** History is the audit trail.
- **Formatting-only commits** go in `.git-blame-ignore-revs` so blame stays useful.
- **Trailers and attribution** follow each repository's own `CLAUDE.md` (it differs today: `mortgage-analytics` adds a
  `Co-Authored-By` trailer; Hyvva forbids one). The platform standard is: the rule is written in the repository and a
  commit-message check enforces it (**new**).
- **The review checklist is the [definition of done](#definition-of-done).** A reviewer, human or AI, works down it.

---

## Release and deploy

1. **Only the owner deploys.** An AI agent prepares the change, runs `cdk diff` or `cdk synth`, and stops. (The-hive's brief
   records the incident: an agent deployed a single stack directly, skipped the script's safety flag and triggered a real
   certificate request for a domain nobody owned.)
2. **`cdk diff` is read before every deploy**, and anything that replaces or deletes a resource with data is called out.
3. **Environments in order:** local, dev, staging, production, in **separate AWS accounts**. Nothing reaches production without passing staging.
4. **Never rename a physical resource** (queue, table, topic names): CloudFormation cannot rename in place, it replaces and loses data.
5. **Database changes are backward-compatible for one release** (expand, then contract): add the column and write to both; later drop
   the old one. A deploy can then be rolled back without a data migration.
6. **Risky features ship behind a feature flag** (AWS AppConfig), with the flag's removal date written down. Foundational work does
   not need a flag; over-flagging has its own cost.
7. **A rollback is a documented, rehearsed step**, not an improvisation.
8. **After deploy, the smoke check runs** ([G-42](#supply-chain-and-operations)) and the alarms are watched for the first minutes.

---

## One command per tier

Every repository exposes the same entry points so nobody has to remember which tool to run (**exists**: `scripts/check.ps1` in `backplane-standards` and in `mortgage-analytics`; a small script in the template; a test holds its steps to the repository, and to CI):

| Command           | Runs                                                                                        |
| ----------------- | ------------------------------------------------------------------------------------------- |
| `./check fast`    | Tier 1: format check, lint, typecheck, conventions, unit tests of what changed, secret scan |
| `./check full`    | Tier 2 locally: everything CI runs, so CI is a confirmation and not a discovery             |
| `./check release` | Tier 4: full mutation, cross-browser, load, oracle comparison, real-database tests          |

(On Windows these are PowerShell scripts, `./scripts/check.ps1 fast`; the names are the same. A step whose tool is not installed is reported as SKIPPED, loudly, never as passed. `release` includes the dependency report, `tools/dependency_report.py`: a known vulnerability fails, a newer version is listed.) The reusable CI workflow calls the same scripts, so what runs
on the machine is what runs in CI.
