# 11. Roadmap

> The build order, what "done" means for each step, the first-week checklist, and what the owner must decide or verify first.

**Read time:** 15 min · **Best for:** knowing what happens next and what is blocking it · **Builds on:** every earlier document · **Status:** a plan, written
2026-11-03; nothing here is built yet

**On this page:** [Principles of the plan](#principles-of-the-plan) · [Phase 0: decisions](#phase-0-decisions) · [Phase 1: standards and the template](#phase-1-standards-and-the-template) ·
[Phase 2: the kit and the serverless host](#phase-2-the-kit-and-the-serverless-host) · [Phase 3: the UI kit](#phase-3-the-ui-kit) · [Phase 4: capabilities](#phase-4-capabilities) ·
[Phase 5: adoption](#phase-5-adoption) · [The first week](#the-first-week) · [Open decisions](#open-decisions) · [Open verifications](#open-verifications) · [How we will know it works](#how-we-will-know-it-works)

---

## Principles of the plan

> **Update (2026-11-03, [ADR 0011](adr/0011-mortgage-first.md)):** the standards are applied to `mortgage-analytics` first, inside its own repository, following [document 12](12-mortgage-first.md). Phases 1 to 4 below describe the extraction that comes **after** that; no other application is rewritten, and the decisions about the platform name and public or private repositories are **no longer blocking**.

1. **Standards first, then code.** A new app's first day must have every gate on, so the standards package and the template come before any library or capability.
2. **Extract, never invent.** Each piece is lifted from a working app, with its tests, then proved by the next app. The reference app (`mortgage-analytics`) is the first adopter, so extraction is verified by deleting its own copy.
3. **One small proof per phase.** Each phase ends with something running that a newcomer could use, not a document.
4. **No phase starts without its primers and ADRs** ([documentation standard](10-documentation-standard.md)).
5. **Inside the CI budget** ([document 9](09-repositories-and-delivery.md#the-ci-minutes-budget)): publishing and expensive checks are local.
6. **Durations are not promised.** Phases are ordered and each has an exit test; the effort depends on how much of the owner's time goes to this versus the applications.

---

## Phase 0: decisions

The work on `mortgage-analytics` ([document 12](12-mortgage-first.md)) needs none of these answers. The decisions in [Open decisions](#open-decisions) become blocking only when the first package is extracted (the platform **name** and **public or private** repositories) or when a second application or a serverless host is started (the **canonical product** and the **idle-cost ceiling** that decides the database host).

**Exit:** the ADRs marked _Proposed_ are _Accepted_ or revised, and the blocking decisions are recorded.

---

## Phase 1: standards and the template

**Goal:** a new application starts with every gate on, the first time it is built.

| #    | Task                                                                                                                                                                                          | Exit test                                                                                                                                          |
| ---- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1.1  | Create the `platform-standards` repository (local first); move these documents and ADRs into it                                                                                               | The README's reading order works from the repository root                                                                                          |
| 1.2  | Lift the build rules (`Directory.Build.props`, `Directory.Packages.props` pattern, `.editorconfig`, `global.json`, analyzers) into a package                                                  | `mortgage-analytics` builds with the package's settings and no local copies                                                                        |
| 1.3  | Generalize the 15 conventions tests into a package and a tiny per-app runner; add the new rules (README per folder, no `Utils`-style names, suppression with a reason, no `double` for money) | `mortgage-analytics` uses the package with the same counts; new rules green or ratcheted                                                           |
| 1.4  | Architecture tests and ratchets: layer dependencies, contract layering, `DateTime.Now`, `dynamic`, no AWS SDK in Domain, one ceiling per ratchet                                              | Zero violations, or recorded ceilings, in `mortgage-analytics`                                                                                     |
| 1.5  | Front-end presets: ESLint config (with the import-boundary rule), Prettier config, stylelint, strict `tsconfig`s                                                                              | `mortgage-analytics/client` uses them; lint, format and typecheck green                                                                            |
| 1.6  | Stryker base configuration and thresholds; a script that runs mutation for chosen projects                                                                                                    | The Domain run reproduces the reference score                                                                                                      |
| 1.7  | The check scripts: `check fast`, `check full`, `check release` (PowerShell), including gitleaks and the dependency report                                                                     | Each runs on a clean clone and reports the gates it ran                                                                                            |
| 1.8  | The reusable CI workflow (`dotnet-angular-ci`), pinned actions, read-only permissions, path filters                                                                                           | `mortgage-analytics` CI calls it, with total minutes no higher than today                                                                          |
| 1.9  | The documentation checks: counts, links, `file:line` citations, README per folder, primer per package, generated `MAP.md`                                                                     | They run in `check full` and fail on a planted error                                                                                               |
| 1.10 | The `app-template` (`dotnet new` template): a "hello" application with one use case, one endpoint, one Angular page, all four layers, all gates, a `docs/start-here.md` and the tutorials     | A new app made from it passes `check full` first time, and a newcomer finishes [the onboarding path](10-documentation-standard.md#onboarding-path) |
| 1.11 | Primers for what Phase 1 adopts: AWS CDK in C#, CDK assertions and cdk-nag, gitleaks, MinVer, `dotnet new` templates                                                                          | Each primer follows the template and is linked from the index                                                                                      |

**Exit:** _create a new application from the template, change one rule test-first, push, and see every gate run_, with a newcomer doing it from the documentation alone.

---

## Phase 2: the kit and the serverless host

**Goal:** the cross-cutting libraries exist once, and one real application runs serverless.

| #   | Task                                                                                                                                                                                                        | Exit test                                                                                    |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| 2.1 | **Evaluate AWS Lambda Powertools for .NET** (logging, metrics, tracing, idempotency) against building our own; record the result in an ADR                                                                  | A decision with a prototype of each option on the same use case                              |
| 2.2 | Logging with PII redaction and the request id (from the two existing redactors), as a library with tests and a primer                                                                                       | Both Hyvva's and the-hive's redaction tests pass against it; a log-template scan test exists |
| 2.3 | Idempotency, rate limiting for a serverless host (not an in-process gate), security headers, `Result<T>` mapping, test support (`TestClock`, builders)                                                      | Each has tests, mutation score at the library threshold, and a library page                  |
| 2.4 | **The serverless host spike:** deploy the mortgage calculation API as a Lambda in a dev account behind API Gateway, with alarms and a canary; measure cold start for the managed runtime and for native AOT | [ADR 0006](adr/0006-lambda-granularity.md) accepted or revised, with measurements            |
| 2.5 | The typed client generator spike: generate a C# and a TypeScript client from the mortgage API's OpenAPI and use them in a test and the Angular app                                                          | One tool chosen, with a primer; the generated client replaces a hand-written one             |
| 2.6 | Version and publish: MinVer tag prefixes, `release.ps1`, the local feed, then private GitHub Packages                                                                                                       | A library is released from a tag and restored by an application                              |
| 2.7 | Property-based testing introduced for money and parsing (FsCheck, fast-check); primers                                                                                                                      | At least one property test in each of Domain and client logic                                |

**Exit:** `mortgage-analytics`'s calculation API runs on Lambda in a dev account, observable and alarmed, using the kit's libraries.

---

## Phase 3: the UI kit

**Goal:** one design system and app shell, used by two applications.

| #   | Task                                                                                                                             | Exit test                                                                   |
| --- | -------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------- |
| 3.1 | Extract design tokens (colour, type, spacing, themes) from the reference app's Blueprint standard and the-hive's design brief    | One token file; both themes meet contrast                                   |
| 3.2 | Extract the app shell: header, account menu, settings page, dialogs, theme and language switches, the translation function       | The template app uses it; its axe, layout-shift and reader-loss checks pass |
| 3.3 | A component gallery with visual regression (Playwright screenshots, one browser, fixed fonts) and an axe scan of every component | The gallery is a tier-2 check in the UI kit repository                      |
| 3.4 | Import boundaries and a "no raw colours" lint rule                                                                               | Violations fail lint                                                        |
| 3.5 | Documentation: a page per component with when to use it, accessibility notes and examples                                        | Every public component is documented                                        |

**Exit:** the template app and `mortgage-analytics` render the same shell from the same package.

---

## Phase 4: capabilities

**Goal:** the first two capabilities, then the rest in order of how many apps need them.

| #   | Task                                                                                                                                                      | Exit test                                                                                                                                |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| 4.1 | [ADR 0009](adr/0009-capability-packaging.md) spike on the **email** capability; write its contract, construct, client, fakes, threat model, SLO, runbooks | An application adds email in under ten lines; the checklist in [document 8](08-capability-modules.md#rules-for-a-capability) is complete |
| 4.2 | **Login** on Cognito: pools, passkeys (verify behaviour with MFA first), sessions list, recovery, account deletion; UI pieces                             | The template app has sign-in with one line; the security checklist passes                                                                |
| 4.3 | Audit trail, settings and preferences, account data (export and delete), legal acceptance                                                                 | Each has the full capability checklist                                                                                                   |
| 4.4 | Billing (Stripe), extracted from the-hive and Hyvva                                                                                                       | Two-tenant tests, idempotency and side-effect tests green                                                                                |
| 4.5 | Feature flags (AppConfig), notifications, files, scheduler, as a second app needs them                                                                    | Each extracted from a working app and proved by a second                                                                                 |

**Exit:** a new application assembled from the template plus login and email, deployed to a dev account, with every gate green and a newcomer able to follow it.

---

## Phase 5: adoption

**Not now** ([ADR 0011](adr/0011-mortgage-first.md)). Existing applications may later decide, one at a time, whether to move onto the platform. Nothing forces a rewrite.

| Application          | Likely path                                                                                                                   |
| -------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| `mortgage-analytics` | First adopter of every phase; its own copies are deleted as each package lands; mortgage calculation becomes a shared service |
| Hyvva or the-hive    | One becomes the canonical multi-tenant site and booking product; the other is retired or mined for capabilities               |
| Veranda (Livenhood)  | Stays TypeScript as a product, or is rebuilt on the platform if it goes to market; the owner decides                          |
| buybright            | Retires its duplicate mortgage maths in favour of the shared calculation service, or is retired                               |

---

## The first week

Mortgage first ([document 12](12-mortgage-first.md#the-work-list)). Each day ends with something committed, test-first, with the documentation and counts updated.

| Day | Do                                                                                                                                            | Done when                                                               |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| 1   | Work-list item 1: a README in each project and top-level folder, a conventions test that fails when one is missing, and a generated `MAP.md`  | The test is green and the docs count the new conventions test           |
| 2   | Items 2 and 3: a pull-request template with the definition of done; `scripts/check.ps1` (`fast`, `full`, `release`) with gitleaks run locally | `./check fast` runs on a clean clone                                    |
| 3   | Item 4: the tripwire tests (floating point in the Domain, suppression needs a reason, log-template scan)                                      | Each is red on a planted violation and green on the code                |
| 4   | Item 5: NetArchTest layer tests                                                                                                               | The layer rules are enforced by a test as well as by project references |
| 5   | Item 6 (start): move the shared calculator pieces out of `features/calculator`; add the ESLint boundary rule                                  | No feature imports another feature; lint fails on a planted import      |

---

## Open decisions

For the owner. Each has a recommendation; **blocking** items stop Phase 1.

| #   | Decision                                                                                | Recommendation                                                                                              | Blocking?                             |
| --- | --------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- | ------------------------------------- |
| 1   | **The platform name** (package names, namespaces, domains)                              | Pick a short, neutral name; the-hive's name is also still open                                              | No (deferred to the first extraction) |
| 2   | **Public or private** kit repositories and packages                                     | Private until there is a reason to open; revisit at launch ([ADR 0004](adr/0004-repositories.md))           | No (deferred to the first extraction) |
| 3   | **Canonical product**: Hyvva or the-hive for sites and booking                          | Hyvva (4,619 files, 1,182 commits, Aurora and RLS) unless the-hive's DynamoDB design is preferred           | Phase 4                               |
| 4   | **Idle-cost ceiling per environment**, which decides Aurora versus Neon versus DynamoDB | State a monthly figure; scale-to-zero hosting if it is near zero ([ADR 0005](adr/0005-data-store.md))       | Phase 2                               |
| 5   | **AWS account structure** (an AWS Organization with dev, staging, prod per app)         | Separate accounts per environment as the security baseline requires; costs and billing set up first         | Phase 2                               |
| 6   | **Error tracking**: CloudWatch only, or add Sentry                                      | CloudWatch only until an app has users ([document 6](06-logging-observability.md#open-decisions))           | No                                    |
| 7   | **Veranda and buybright**: keep as TypeScript, rewrite, or retire                       | Decide by whether each goes to market; do not rewrite speculatively                                         | Phase 5                               |
| 8   | **One sign-in across all apps** (a shared user directory) or per-app pools              | Per-app pools ([ADR 0003](adr/0003-copy-per-app.md)) unless single sign-on is a product requirement         | Phase 4                               |
| 9   | **Who reads the legal documents** for each product                                      | A qualified person, before launch                                                                           | Before launch                         |
| 10  | **Commit and attribution policy** across repositories (the repositories differ today)   | One rule written in each `CLAUDE.md` and checked ([quality gates](03-quality-gates.md#commits-and-reviews)) | No                                    |

---

## Open verifications

Claims this plan relies on that have **not** been confirmed against a running system. Each is a small task with a named place to check.

| #   | Claim to verify                                                                                                               | How                                                                                                                                                                                                                                                                                                                                              | Needed by |
| --- | ----------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------- |
| 1   | A reusable workflow in a **private** repository can be called from another private repository of the same owner               | Try it with two scratch repositories; read GitHub's "Access" setting for Actions                                                                                                                                                                                                                                                                 | Task 1.8  |
| 2   | `.globalconfig` shipped in a NuGet package can carry the **formatting** (IDE0055) rules, not only analyzer severities         | Package a test config and build a sample                                                                                                                                                                                                                                                                                                         | Task 1.2  |
| 3   | Cognito passkeys and MFA together: the reference says passkeys can satisfy MFA; a community post says they cannot             | Configure a real Essentials-tier pool in a dev account and test sign-in                                                                                                                                                                                                                                                                          | Task 4.2  |
| 4   | AWS Lambda Powertools for .NET supports .NET 10 and the features we need (logging, metrics, tracing, idempotency)             | Read its current release notes; build the prototype                                                                                                                                                                                                                                                                                              | Task 2.1  |
| 5   | Native AOT with ASP.NET Core Minimal APIs on the `dotnet10` Lambda runtime, and the measured cold start                       | The spike in Task 2.4                                                                                                                                                                                                                                                                                                                            | Task 2.4  |
| 6   | NetArchTest and the mutation tools work on .NET 10 (Stryker.NET already runs on it in the reference app)                      | Run both on `mortgage-analytics`                                                                                                                                                                                                                                                                                                                 | Task 1.4  |
| 7   | MinVer supports per-package tag prefixes in a monorepo as assumed                                                             | A scratch repository with two packages                                                                                                                                                                                                                                                                                                           | Task 2.6  |
| 8   | The free-tier figures (2,000 Linux minutes, 500 MB and 1 GB of packages) still hold when we start                             | Re-read GitHub's billing pages ([Actions](https://docs.github.com/en/billing/managing-billing-for-your-products/managing-billing-for-github-actions/about-billing-for-github-actions), [Packages](https://docs.github.com/en/billing/managing-billing-for-your-products/managing-billing-for-github-packages/about-billing-for-github-packages)) | Phase 1   |
| 9   | The typed-client generator choice (NSwag, Kiota or openapi-typescript) produces clean C# and TypeScript from our contract     | The spike in Task 2.5                                                                                                                                                                                                                                                                                                                            | Task 2.5  |
| 10  | The idempotency tests in the-hive's billing and booking services exist (the repositories do; replay tests were not confirmed) | Read their test projects                                                                                                                                                                                                                                                                                                                         | Phase 4   |

---

## How we will know it works

Measures, checked at the end of each phase:

| Measure                                                             | Target                                                                                       |
| ------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| Time from `dotnet new` to a green `check full` on a new app         | Under one hour on a prepared machine                                                         |
| Lines an app writes to add login or email                           | Under ten for the defaults                                                                   |
| A newcomer completing the onboarding path using only the repository | Within a few days, with no questions unanswered by the docs                                  |
| Share of rules that are machine-enforced rather than review rules   | Rising; every review rule listed with a plan to automate it                                  |
| Duplicated cross-cutting code across applications                   | Falling toward zero for redaction, idempotency, rate limiting                                |
| CI minutes used per month                                           | Within the allocation in [document 9](09-repositories-and-delivery.md#the-ci-minutes-budget) |
| Mutation score of the Domain and each kit library                   | At or above its threshold, never silently lowered                                            |
| Documentation counts, links and citations                           | The docs check is green on every push                                                        |
