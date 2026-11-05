# 12. Mortgage first: bringing `mortgage-analytics` up to the standards

> No application is rewritten yet. The standards are applied to `mortgage-analytics` first, inside its own repository, and only afterwards lifted into shared packages for the next app to use.

**Read time:** 15 min · **Best for:** what to do next, in what order, and how far this app already is from the standards · **Builds on:** [Quality gates](03-quality-gates.md), [Testing strategy](04-testing-strategy.md), [Architecture](02-architecture.md) · **Decision:** [ADR 0011](adr/0011-mortgage-first.md) · **Measured:** 2026-11-03 on `main` at `52c8eec`

**On this page:** [What changes in the plan](#what-changes-in-the-plan) · [What the platform name is](#what-the-platform-name-is) · [Already met](#already-met) · [The gaps](#the-gaps) · [The work list](#the-work-list) · [What waits](#what-waits) · [How each item is done](#how-each-item-is-done)

---

## What changes in the plan

The roadmap ([document 11](11-roadmap.md)) assumed the standards would first be packaged and then adopted. The owner's direction is simpler and safer:

1. **No rewrites** of Hyvva, the-hive, Veranda or buybright for now.
2. **`mortgage-analytics` follows the new standards first**, in its own repository, with its own tests and documentation, so the rules are proved on one real, working application before they become shared packages.
3. **Extraction comes after** (the roadmap's phase 1 tasks 1.2 to 1.10) and is verified by deleting this app's own copy, exactly as planned. The shared repositories are still the goal; the order is what changes.

This costs less (no new repositories or pipelines yet), keeps the CI-minutes budget flat, and means every rule has already survived contact with real code before anyone else has to follow it.

---

## What the platform name is

A question that came up: "is the platform name the mini repositories, or a new application?"

**Neither is a new application.** The "platform" in these documents is only our name for the shared tooling: the standards, the libraries and the capabilities. The **name** is just the **prefix** those shared pieces carry, the way a company name prefixes its packages:

| Piece          | With a name such as `Acme` it would be called |
| -------------- | --------------------------------------------- |
| NuGet packages | `Acme.Standards.Conventions`, `Acme.Logging`  |
| npm packages   | `@acme/eslint-config`, `@acme/ui`             |
| Repositories   | `acme-standards`, `acme-kit`, `acme-ui`       |
| C# namespaces  | `Acme.Logging`, `Acme.Email.Client`           |

So the "mini repositories" you described _are_ the platform repositories; the name only labels them. It matters on the day the first package is published (package names are hard to change afterwards). **It does not matter now**, because nothing is being published yet. The decision is therefore **not blocking**: it is deferred to the day the first package is extracted. (The-hive has its own unrelated unresolved product name; that is a product, not this tooling.)

The folder is still called `C:\Dev\platform`. If "platform" is confusing next to the-hive's "platform", it can be renamed to something neutral such as `standards` in one commit; nothing depends on the folder name yet.

---

## Already met

Measured on `mortgage-analytics` (source, tests, client; generated code and `bin`/`obj` excluded). Many of the hardest bans already hold.

| Standard                                                                                        | Measured today                                                                                        |
| ----------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------- |
| Warnings are errors, style in the build, nullable, XML docs on public API                       | On ([`Directory.Build.props`](primers/msbuild-and-build-rules.md))                                    |
| Locked, centrally managed packages                                                              | On (`Directory.Packages.props`, `packages.lock.json`, locked-mode restore in CI)                      |
| The Domain depends on nothing                                                                   | Its project file has no package references                                                            |
| No `DateTime.Now` or `UtcNow` in production code                                                | **0** (time comes from `TimeProvider`)                                                                |
| No `dynamic`                                                                                    | **0**                                                                                                 |
| No `#pragma warning disable`                                                                    | **0** in `src` and `tests`; 1 `SuppressMessage`                                                       |
| No lint or type suppressions in the client                                                      | **1** (`eslint-disable`, `@ts-ignore` or `@ts-expect-error` in `src` and `e2e`)                       |
| No `console.log` in the client                                                                  | **0** outside specs                                                                                   |
| No explicit `any` in the client                                                                 | **0** (a text search for "any" finds five words inside comments; the lint rule would catch real ones) |
| `HttpClient` only in `core/`                                                                    | Yes: four files, `app.config.ts`, `core/auth` (two), `core/dev`                                       |
| No dumping-ground names (`Util`, `Helper`, `Manager`, `Common`, `Misc`)                         | **0** files or folders                                                                                |
| Conventions tests                                                                               | 15 (assertions, one type per file, no nesting, three-letter names, `var`)                             |
| Coverage                                                                                        | Gated at 85% (backend line and branch, Jest all four metrics)                                         |
| Mutation testing                                                                                | Domain gated at 95%; run in CI                                                                        |
| Contract snapshot, independent oracle, load check, e2e, axe, layout, reader-loss, cross-browser | All present                                                                                           |
| Format everywhere, lint, strict typecheck of specs and e2e                                      | All present                                                                                           |

---

## The gaps

What the new standards ask for that this app does not yet have. Each is measured or observed, not assumed.

| #   | Gap                                                                                   | Evidence                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| --- | ------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| G1  | **No README in most projects and top-level folders**                                  | 15 of 18 checked folders lack one: the four `src` projects, the six test projects, `client/src/app/core`, `shared`, `features`, `client/e2e`, `deploy`. `client`, `docs`, `tools` have one                                                                                                                                                                                                                                                 |
| G2  | **Features import other features** (the frontend boundary rule)                       | 5 imports in 3 files: `compare-page.component.ts` and `comparison.store.ts` import from `features/calculator`; `refinance-results.component.ts` imports `rate-what-if` from it                                                                                                                                                                                                                                                             |
| G3  | **No automatic frontend boundary rule**                                               | No `no-restricted-imports` or boundary rule in `client/eslint.config.js`; the rule is followed by habit                                                                                                                                                                                                                                                                                                                                    |
| G4  | **Floating point inside the Domain** ([ADR 0007](adr/0007-money.md): never for money) | two places (four lines) use `double`: `MarketScenario.cs:63` and `StrategyRates.cs:157-159` (growth with fractional exponents, converted back to `decimal`); `DecimalMath.Pow` handles only whole exponents. Not money itself, but the standard needs a stated, tested exception                                                                                                                                                           |
| G5  | **Limits written in several places**                                                  | On the server the rate limit is one named constant (`LoanTerms.MaxAnnualInterestRate = 30m`) that the validators use, which is the right shape. The client repeats a literal `max: 30` in 8 field definitions across four files (`loan-fields.ts` twice, `rent-settings`, `saving-up`, `second-loan` three times; several are interest-rate-style boxes), and `RentOrBuyAssumptions` and `MarketAssumptions` use bare `30` in their guards |
| G6  | **Validation messages have no stable code** (closed 2026-11-04)                       | The client did not translate server messages at all (shown as sent, so a Spanish user read English), and with no code a reworded message could never be found again                                                                                                                                                                                                                                                                        |
| G7  | **Mutation score gates stop at the Domain**                                           | Application: `break 0` (reports only); Angular logic: `break null`                                                                                                                                                                                                                                                                                                                                                                         |
| G8  | **No architecture tests beyond the contract-layering one**                            | Layers are held by project references (good), but nothing tests "no EF Core in Application", "no AWS SDK in Domain" or bans as ratchets                                                                                                                                                                                                                                                                                                    |
| G9  | **No suppression-needs-a-reason check**                                               | 1 `SuppressMessage` and 1 client suppression exist; nothing would stop a bare one                                                                                                                                                                                                                                                                                                                                                          |
| G10 | **No complexity or method-length limits**                                             | Not measured; the largest production files are 461, 406, 310, 309 and 276 lines (`AmortizationMapper.cs`, two contract files, `AccountEndpoints.cs`, a validator)                                                                                                                                                                                                                                                                          |
| G11 | **No property-based tests**                                                           | Invariants are tested with examples and the independent oracle; no generator-driven tests for schedules, rounding or formatters                                                                                                                                                                                                                                                                                                            |
| G12 | **Docs checks cover counts only**                                                     | `doc_counts.py` and `doc_refs.py` verify counts, links and citations here; `platform-standards` now has the same checks as `tools/doc_checks`, with a map, README and primer check besides, and they pass on this application with its configuration, so it can drop its copies when it adopts them (board task 19)                                                                                                                        |
| G13 | **No secret scan, no one-command checks**                                             | No gitleaks; the steps live in memory notes and CI, not in `check fast/full/release` scripts                                                                                                                                                                                                                                                                                                                                               |
| G14 | **No log-template scan**                                                              | Request logging is good and tested; nothing prevents a `{Email}` placeholder or an interpolated log string                                                                                                                                                                                                                                                                                                                                 |
| G15 | **No accessibility coverage guard**                                                   | axe runs on the pages listed in the spec; nothing fails when a new route is added without being scanned                                                                                                                                                                                                                                                                                                                                    |
| G16 | **Error and empty states not covered on Compare, Refinance, Strategy**                | Listed as "not covered" in the October audit                                                                                                                                                                                                                                                                                                                                                                                               |
| G17 | **Primers do not exist for several packages**                                         | Missing: ASP.NET Core Identity and cookies and Data Protection, Fido2 (WebAuthn), MailKit, health checks, RxJS                                                                                                                                                                                                                                                                                                                             |
| G18 | **Open hardening items** (row A9 of the UI and UX backlog)                            | **Closed 2026-11-04.** Were: a 10-passkey cap checked only at the start of the ceremony, opportunistic SMTP TLS, unencrypted signing keys on the volume, expired session rows pruned only on listing, no per-recipient mail cap                                                                                                                                                                                                            |
| G19 | **Open items waiting on the owner** (UI backlog)                                      | U10 NVDA pass, U11 Spanish "C e I", U13 a real phone                                                                                                                                                                                                                                                                                                                                                                                       |
| G20 | **No pull-request checklist**                                                         | The definition of done lives in these documents, not in the repository's workflow                                                                                                                                                                                                                                                                                                                                                          |

Port contract suites ([Testing strategy, row 15](04-testing-strategy.md#the-catalog)) do **not** apply here: the repository tests run against a real SQLite database and PostgreSQL, so there is no in-memory fake whose fidelity needs proving.

---

## The work list

Ordered by value for effort and by risk. Each item is done **test-first**, committed on its own, with the docs updated, and checked with `dotnet test`, `npm run lint`, `npm run typecheck`, and `python tools/doc-check/doc_counts.py` as the repository's conventions require. Sizes: **S** under an hour, **M** a session, **L** several.

| Order | Item                                                                                                                                                                                           | Closes      | Size   | CI effect                |
| ----- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------- | ------ | ------------------------ |
| 1     | A README in each project and top-level folder; a conventions test that fails when one is missing; a generated `MAP.md`                                                                         | G1          | S      | Seconds                  |
| 2     | A pull-request template with the [definition of done](03-quality-gates.md#definition-of-done)                                                                                                  | G20         | S      | None                     |
| 3     | `scripts/check.ps1` with `fast`, `full`, `release`, and gitleaks run locally                                                                                                                   | G13         | S      | None (local)             |
| 4     | Tripwire tests: no `double` or `float` in the Domain except a documented, ceilinged list; suppression needs a reason; log-template scan                                                        | G4, G9, G14 | S to M | Seconds                  |
| 5     | NetArchTest layer tests: Application has no EF Core or ASP.NET, Domain has no packages, no AWS SDK in Domain or Application                                                                    | G8          | S      | Seconds                  |
| 6     | Move the shared calculator pieces out of `features/calculator` into `shared/` (or `core/state`), then add the ESLint boundary rule, with a lint test that a planted cross-feature import fails | G2, G3      | M      | Seconds                  |
| 7     | An accessibility coverage guard: every route in `app.routes.ts` appears in an axe spec, or the test says why not                                                                               | G15         | S      | Seconds                  |
| 8     | Documentation checks: relative links and anchors, `file:line` citations in guides 14 and 15, README presence, `MAP.md` freshness, all in the existing docs CI job                              | G12         | M      | Same job, no new minutes |
| 9     | Measure the Application and Angular mutation scores locally and set `break` thresholds just under what is measured; record the dates                                                           | G7          | M      | None (local)             |
| 10    | Property-based tests: FsCheck for schedule invariants and rounding, fast-check for formatters and parsers                                                                                      | G11         | M      | A few seconds            |
| 11    | Complexity and method-length analyzers: measure first, then set thresholds as a ratchet                                                                                                        | G10         | M      | Build time, slight       |
| 12    | One source for each limit: name the Domain constants, have the validators use them, and serve or generate the client's limits; a test that a client limit equals its server limit              | G5          | M to L | Seconds                  |
| 13    | Primers for the packages that lack one, added to the platform folder (later the standards repository)                                                                                          | G17         | M      | None                     |
| 14    | Error and empty-state e2e tests for Compare, Refinance and Strategy                                                                                                                            | G16         | M      | A little e2e time        |
| 15    | Close the hardening items in row A9, one commit each, each with a test that failed first                                                                                                       | G18         | M to L | Seconds                  |
| 16    | Stable validation codes beside messages, with the client translating by code                                                                                                                   | G6          | L      | Seconds; contract change |
| 17    | The owner's items: NVDA pass, Spanish "C e I", a real phone                                                                                                                                    | G19         | owner  | None                     |

Items 1 to 5 are small, make the standards visible, and carry no risk to the running app. Items 6 and 12 touch real behaviour and get the most care. Item 16 changes the public contract (the approved OpenAPI snapshot is re-approved on purpose) and is last because it only pays off when a second consumer exists or the Spanish text starts to drift.

---

## Progress

Items 1 to 6 were done on 2026-11-03 in `mortgage-analytics`, one commit each. Items 1 to 5 are pushed; item 6 is committed locally. Every other item is still to do.

| Item | What was done                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          | Closes                                            | Commit               |
| ---- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------- | -------------------- |
| 1    | A README in all 15 folders that lacked one (and in `scripts/`); `ReadmeConventionTests` (red on exactly those 15 first); a generated `MAP.md` whose freshness is a test in the existing `unittest` step; a drift guard between the C# and Python folder lists                                                                                                                                                                                                                                          | G1                                                | `77ed4f4`, `dad680a` |
| 2    | `.github/pull_request_template.md` with the 13-point definition of done, and a test that keeps its checks                                                                                                                                                                                                                                                                                                                                                                                              | G20                                               | `62d75ee`            |
| 3    | `scripts/check.ps1` with `fast`, `full` and `release` tiers, run end to end in Windows PowerShell 5.1; a test holds its steps to the repository. **Two tools are not installed on this machine (ruff, gitleaks), so those three fast steps report SKIPPED here**                                                                                                                                                                                                                                       | G13 (secret scan runs once gitleaks is installed) | `01f1e69`            |
| 4    | Tripwires: floating point in the Domain as a ratchet (2 files, 7 keywords), every suppression needs a reason (red on the one line in `xlsx.ts`, now fixed), log templates never name personal data and are never built by hand. 25 new convention tests                                                                                                                                                                                                                                                | G4 (now a ratchet), G9, G14                       | `184023c`            |
| 5    | `LayerConventionTests`: project references, packages and `using` directives checked against the dependency rule, with each rule proved on a bad sample. **Done by reading project files and `using` directives instead of NetArchTest** (no new package, no lock-file churn)                                                                                                                                                                                                                           | G8                                                | `0bd1f14`            |
| 6    | Moved the loan form, buy views, rate what-if, owner tax boxes, `CalculatorStore` and `price-scenarios` down to `shared/` and `core/` with one import-rewriting script (no hand edits but `wholeYearsOf`); ESLint `no-restricted-imports` layer rules built from the feature folders; `layers.spec.ts` proves them on 14 bad and good imports. **The per-script size warning moved from 410 to 420 kB** because the build merged a 6 kB chunk into the main script (first download unchanged at 859 kB) | G2, G3                                            | `523a35d`            |

The numbers moved: conventions tests 15 to 52, .NET tests 1,896 to 1,933, Jest 1,912 to 1,928 (about 4,370 in all). All 508 Chromium e2e tests pass after item 6. The documentation checks, Prettier, ruff (installed in a scratch environment), a warnings-as-errors Release build and the full non-PostgreSQL .NET run are green.

Two things worth knowing:

- Another session had uncommitted edits in the working tree (`client/README.md`, `docs/02-commands.md`, `docs/README.md`, `tools/README.md`, `tools/doc-check/README.md`, and an untracked `tools/load-check/README.md`). They were left alone and are not in any of these commits.
- Items 6 onwards touch behaviour (moving shared calculator pieces between folders, one source for each limit, the hardening items) and will each need care and a visible check in the running app.

---

## What waits

Not part of this focus, deliberately:

- Creating the shared repositories and packages (extraction follows once the rules have proved themselves here).
- Moving `mortgage-analytics` to Lambda (the serverless spike in [document 11](11-roadmap.md#phase-2-the-kit-and-the-serverless-host) is separate from the standards).
- Rewriting or restructuring any other application.
- Choosing the platform name, the public-or-private question and the canonical-product question: none blocks work on this app.

---

## How each item is done

The same loop for every item, so the work stays predictable:

1. **State the rule and its reason** in one sentence.
2. **Write the test that fails** (for a rule: a scan that is red on today's violations, with a "scanner is alive" assertion; for a behaviour: the usual red test).
3. **Make it green** with the smallest change; for a rule with many violations, adopt a [ratchet](primers/netarchtest-and-ratchets.md) at today's count instead of a flag day.
4. **Run the repository's own checks** (the list under the table above) and fix the counts in the README and docs.
5. **Update the documentation** in the same commit: the testing guide, the counts, and where relevant a primer or an ADR.
6. **Commit with a message that says why**; docs-only commits use `[skip ci]`.
7. **Record the measurement** (a number, a date) in this document's table when a gap closes, so progress is visible.
