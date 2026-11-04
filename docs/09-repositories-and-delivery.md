# 9. Repositories and delivery

> Which repositories exist and when each is created, how a package gets from a laptop to an application, and how all of it fits
> inside a free GitHub allowance of build minutes.

**Read time:** 15 min · **Best for:** creating a repository, publishing a package, planning CI · **Builds on:**
[Strategy](00-strategy.md), [Architecture](02-architecture.md#across-repositories), [Quality gates](03-quality-gates.md#the-four-tiers)

**On this page:** [Repositories](#repositories) · [When a repository is created](#when-a-repository-is-created) · [Inside a kit repository](#inside-a-kit-repository) ·
[Versioning and releases](#versioning-and-releases) · [The package feed](#the-package-feed) · [Developing a kit change against an app](#developing-a-kit-change-against-an-app) ·
[The CI-minutes budget](#the-ci-minutes-budget) · [The reusable pipeline](#the-reusable-pipeline) · [Keeping dependencies fresh](#keeping-dependencies-fresh) ·
[Keeping applications on the platform](#keeping-applications-on-the-platform) · [Windows notes](#windows-notes)

---

## Repositories

Few repositories, each with a clear job. Names are placeholders until the [platform name](11-roadmap.md#open-decisions) is chosen.

| Repository              | Job                                           | Contains                                                                                                                                                   | Published as                                                                                                                  |
| ----------------------- | --------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| `platform-standards`    | The rulebook and the enforcement              | These documents, ADRs, build props, analyzers, `.editorconfig`, conventions tests, lint and format presets, Stryker base, reusable pipeline, check scripts | NuGet (`Brand.Standards.*`), npm (`@brand/eslint-config`, `@brand/prettier-config`, `@brand/tsconfig`), the reusable workflow |
| `platform-kit`          | .NET libraries with no data and no deployment | Logging and redaction, request id, clock, idempotency, rate limiting, security headers, results, test support                                              | NuGet, one package per library                                                                                                |
| `platform-ui`           | The design system and Angular libraries       | Tokens, components, app shell, i18n, theme, a component gallery with visual tests                                                                          | npm, one package per library                                                                                                  |
| `platform-capabilities` | Capability modules                            | One folder per capability ([document 8](08-capability-modules.md#anatomy-of-a-capability))                                                                 | NuGet and npm per capability part                                                                                             |
| `app-template`          | The starting copy of an application           | A solution wired to everything above                                                                                                                       | A `dotnet new` template package                                                                                               |
| each application        | The product                                   | Its own domain, screens, configuration                                                                                                                     | Deployed, not published                                                                                                       |

`mortgage-analytics` stays its own repository and is the first adopter. `Hyvva`, the-hive, Veranda and buybright decide later whether to move onto the platform
or stay as they are; nothing here forces a rewrite.

---

## When a repository is created

A repository costs a pipeline, a README, a release routine and attention. Create one only when something needs to live in it.

| Repository              | Created when                                                                                      | Before that                                 |
| ----------------------- | ------------------------------------------------------------------------------------------------- | ------------------------------------------- |
| `platform-standards`    | **First** (phase 1): the build props and conventions tests are lifted out of `mortgage-analytics` | These documents live in `C:\Dev\platform`   |
| `app-template`          | Phase 1, right after: the proof that a new app starts with every gate on                          |                                             |
| `platform-kit`          | Phase 2: the first library is extracted (logging and redaction, from two existing copies)         | Libraries stay in the app that has them     |
| `platform-ui`           | Phase 3: the second app needs the shell and tokens                                                | The UI lives in `mortgage-analytics`        |
| `platform-capabilities` | Phase 4: the second app needs login or email                                                      | The capability stays in the app that has it |

**Monorepo inside each kit repository.** One repository holds many packages, so one pipeline, one history and one place to change a library and its
users together. A package is versioned and published on its own.

---

## Inside a kit repository

```
platform-kit/
  README.md  MAP.md  CLAUDE.md          what it is, a generated map, the rules for agents working here
  Directory.Build.props  Directory.Packages.props  global.json  .editorconfig     inherited from standards
  src/
    Brand.Logging/  Brand.Logging.Tests/            a library and its tests side by side
    Brand.Idempotency/  Brand.Idempotency.Tests/
    ...
  docs/
    primers/  adr/  libraries/<name>.md             one page per library: what, why, how, example
  scripts/
    check.ps1  release.ps1  deps.ps1  map.ps1       the one-command entry points
```

Rules for a library:

1. **One purpose, no business rules, no data.** If it has data or a deployment, it is a [capability](08-capability-modules.md).
2. **Depends on as little as possible** and never on another library of ours unless listed in its README (kept acyclic by a test).
3. **Targets the current LTS** and nothing older, so no multi-targeting maze.
4. **Public API is small and documented** (XML summaries, enforced); everything else is `internal`.
5. **Has the same gates as an application,** including mutation testing: a library used by many is where a surviving mutant costs the most.
6. **Has a page in `docs/libraries/`** and, if it introduces a tool, a primer.

---

## Versioning and releases

- **Semantic versioning per package.** Patch for fixes, minor for compatible additions, major for breaking changes. The public API surface
  is compared automatically (a public-API analyzer, or the OpenAPI diff for services) so a breaking change cannot ship as a minor.
- **The version comes from a git tag, not from a file edited by hand:** `Brand.Logging/v1.4.0`. A tool such as **MinVer** reads the tag
  (with a per-package tag prefix) and stamps the package. The tag _is_ the release record.
- **A release is one script,** `./scripts/release.ps1 Brand.Logging minor`, which:
  1. refuses to run on a dirty tree or a non-`main` branch,
  2. runs `./check release` (tier 4) for that package and its dependents,
  3. tags, packs, and pushes the package to the feed,
  4. appends to the package's changelog and prints the commit to review.
- **A changelog per package** says what changed and what an application must do to upgrade. It is part of the definition of done.
- **Pre-release versions** (`1.5.0-rc.1`) are used to try a change in one application before it is released to all.

---

## The package feed

Where packages live so that an application's build can restore them. Checked 2026-11-03 against GitHub's billing documentation.

| Option                                                                            | Cost and limits                                                                                                        | Good                                                                            | Costs                                                                                                                                       |
| --------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| **GitHub Packages, private** (proposed start)                                     | GitHub Free: 500 MB storage and 1 GB data transfer a month; transfer inside Actions with `GITHUB_TOKEN` does not count | NuGet and npm in one place; same account; access follows repository permissions | A personal access token with `read:packages` on each dev machine; 500 MB is plenty for small libraries but is shared with Actions artifacts |
| GitHub Packages, public                                                           | Free for public packages                                                                                               | No authentication to restore; no quota worries                                  | The code is public                                                                                                                          |
| A **local folder feed** (a directory used as a NuGet source, `npm pack` tarballs) | Free                                                                                                                   | Zero setup; works offline; fine for one developer                               | Not shared with CI or another machine without copying                                                                                       |
| AWS CodeArtifact                                                                  | Pay per use                                                                                                            | Lives next to the rest of the AWS setup                                         | One more service to run and pay for                                                                                                         |

**Proposal:** start with a **local folder feed** while the first library is shaped (phase 2), then move to **private GitHub Packages** once an application's
CI needs to restore it. Keep the kit repositories private until there is a reason to open them (they describe hardened security patterns). The decision on public
versus private is the owner's and is recorded as an [ADR](adr/0004-repositories.md).

Authentication details, so a newcomer does not lose a day: NuGet uses a `nuget.config` with the feed URL and a token read from an environment variable (never
committed); npm uses a project `.npmrc` with `//npm.pkg.github.com/:_authToken=${GITHUB_TOKEN}`. The template ships both files with the variable, not the value.

---

## Developing a kit change against an app

A change to a library should be tried in a real application **before** it is released. Three ways, from simplest:

1. **Local feed.** Pack the library into a local folder and add that folder as the first NuGet source; the app restores the new version.
   `./scripts/release.ps1 -Local` does it with a version such as `1.5.0-local.3`.
2. **Project reference override.** The application's `Directory.Build.props` has a switch: when `UseLocalKit=true` (set in a git-ignored
   `Directory.Build.local.props`), selected `PackageReference`s become `ProjectReference`s to `C:\Dev\platform-kit\src\...`, so you debug into the library in one solution.
3. **npm.** Use `npm link`, or an npm workspace at the same level, for UI libraries.

The rule that makes this safe: **never commit the override.** A check in tier 1 fails if `UseLocalKit` or a local feed path is staged.

---

## The CI-minutes budget

GitHub Free gives the account **$12 of included Actions usage a month**, which GitHub's page expresses as money, not minutes. At the standard Linux rate
of $0.006 a minute that is **about 2,000 Linux minutes**; Windows runners cost about 1.7 times as much and macOS about 10 times
([billing page](https://docs.github.com/en/billing/managing-billing-for-your-products/managing-billing-for-github-actions/about-billing-for-github-actions)).
**Public repositories using standard runners are free.** The owner has said CI time is limited, and `mortgage-analytics` became private on 2026-10-31, so every
run now draws on this allowance. The platform is designed to live inside it.

**Policy:**

1. **Linux runners only.** No Windows or macOS runner without a written reason.
2. **Tier 2 per push stays under ten minutes per repository,** with caches (packages, npm, Playwright browsers) and `concurrency: cancel-in-progress`
   so a newer push cancels an older run.
3. **Path filters:** a documentation-only change runs nothing (`[skip ci]` or `paths-ignore`); a change to one package runs that package and its dependents.
4. **Slow checks move down a tier, not up:** mutation, cross-browser, Lighthouse, load, the oracle and database-container tests run on a schedule or on the
   machine ([document 3](03-quality-gates.md#the-four-tiers)). This is already how `mortgage-analytics` works for everything but the domain mutation run.
5. **A weekly schedule, not a per-push run,** for dependency advisories (Hyvva and the-hive both moved theirs there for this reason).
6. **No new workflow without a minutes estimate** in its description and a line in this table.
7. **Release is local** (`release.ps1`), so publishing a package costs no minutes. A tag-triggered workflow is an option only if it stays under a few minutes.
8. **Watch the meter:** the billing page is checked monthly, and the figures below are revised from real usage.

**Proposed monthly allocation of the 2,000 minutes** (to be corrected from measurement):

| Repository                        | Pushes a month (est.) | Minutes each | Monthly minutes | Notes                                               |
| --------------------------------- | --------------------- | ------------ | --------------- | --------------------------------------------------- |
| Application in active development | 60                    | 10           | 600             | Tier 2 only                                         |
| A second application              | 30                    | 10           | 300             |                                                     |
| `platform-standards`              | 20                    | 5            | 100             | Conventions tests and lint                          |
| `platform-kit`                    | 20                    | 6            | 120             | Unit tests and mutation of changed packages locally |
| `platform-ui`                     | 15                    | 8            | 120             | Component tests, gallery screenshots                |
| `platform-capabilities`           | 20                    | 8            | 160             | CDK assertions and unit tests, no deploys           |
| Scheduled (all repos)             | 4 weeks               | 30           | 120             | Advisories, Lighthouse                              |
| **Reserve**                       |                       |              | **about 480**   | Retries, a busy month                               |
| **Total**                         |                       |              | **2,000**       |                                                     |

---

## The reusable pipeline

Each application's pipeline file should be a few lines calling one shared definition, so improving the pipeline once improves it everywhere.

```yaml
# .github/workflows/ci.yml in an application
name: CI
on:
  push: { branches: [main], paths-ignore: ['**/*.md', 'docs/**'] }
  pull_request:
jobs:
  ci:
    uses: <owner>/platform-standards/.github/workflows/dotnet-angular-ci.yml@v1
    with:
      dotnet-solution: Acme.sln
      client-directory: client
```

The shared workflow (in `platform-standards`) runs `./scripts/check.ps1 full` equivalents: restore in locked mode, build with warnings as errors, backend tests
with coverage, frontend lint, typecheck and Jest, end-to-end on the production build, the docs check, and the container smoke build. It uploads coverage and reports
a summary. It pins every third-party action to a commit and requests read-only permissions.

**To verify before relying on it:** that a workflow in a _private_ repository can be called from another private repository of the same owner (GitHub has an
"Access" setting under Actions for this); if not, the standards repository must be public or the workflow copied by the template. Listed in
[open verifications](11-roadmap.md#open-verifications).

---

## Keeping dependencies fresh

Dependabot and CodeQL were turned down for `mortgage-analytics` because of the minutes limit, so freshness is **a local routine**, not a bot:

- `./scripts/deps.ps1` (monthly) runs `dotnet list package --outdated --include-transitive`, `dotnet list package --vulnerable --include-transitive`, `npm outdated`
  and `npm audit`, and prints a short report.
- **Patch and minor updates** are batched into one change, tested with `check full`, and merged.
- **Major updates** (a new .NET LTS, a new Angular major) are an ADR and are done in the template and the kits **first**, then rolled to applications, so
  every application upgrades through the same tested path. Angular and .NET move on their own schedules (Angular roughly every six months, .NET every year, with an LTS every other year).
- The vulnerability scan (G-40) also runs weekly in CI because advisories appear without any code change.

---

## Keeping applications on the platform

A template is a copy, so it drifts. The platform limits drift by design:

1. **Make the template thin.** Everything that can be a package (build props, analyzers, lint presets, the pipeline, test helpers) _is_ a package, so an application
   gets improvements by bumping a version. Only genuinely per-application files live in the template.
2. **A drift report:** `./scripts/template-diff.ps1` compares an application's template-owned files with the template's current version and lists differences.
3. **An upgrade guide per major version** of the standards and the template, with the exact steps.
4. **A version report** across applications ([document 8, versioning](08-capability-modules.md#versioning)).

---

## Windows notes

The owner develops on Windows 11 with Git Bash and PowerShell. So:

- Scripts are **PowerShell** (`.ps1`) with the same names a Linux CI uses through `pwsh`, which is installed on GitHub's Ubuntu runners.
- Use forward slashes in paths inside scripts and `git` commands; Git Bash rewrites `/paths` in some Docker arguments (`MSYS_NO_PATHCONV=1`).
- .NET SDKs install side by side with `winget`; Node versions with `nvm-windows`; a repository's `global.json` and `.nvmrc` say which are required.
- Line endings are normalized by `.gitattributes`; shell scripts stay `LF`.
