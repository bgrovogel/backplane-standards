# 0. Strategy

> Build the common parts once, as versioned capabilities with the quality gates built in, and assemble every new
> application from them.

**Read time:** 15 min · **Best for:** the whole picture before anything else · **Status:** decided by the owner on
2026-11-03 unless a row says "proposed"

**On this page:** [The goal](#the-goal) · [Decisions made](#decisions-made) · [What we found](#what-we-found-in-the-five-existing-apps) ·
[Library, service, template: the vocabulary](#library-service-template-the-vocabulary) ·
[The idea: capability modules](#the-idea-capability-modules) · [One shared copy or one copy per app](#one-shared-copy-or-one-copy-per-app) ·
[What we standardize](#what-we-standardize) · [What we do not do](#what-we-do-not-do) · [Risks](#risks)

---

## The goal

When we start a new application, the work should be the application's own idea: its screens, its rules, its domain.
Sign-in, email, billing, audit trail, logging, rate limiting, the design system, the test setup, the lint rules and the
pipeline already exist, already work, and already meet the standard. "No major work" is realistic as **hours of
configuration** (a brand name, a domain, an email template), not zero.

A second goal is as important: **quality is not optional and not remembered, it is enforced.** Every rule in these
documents is checked by the build or says that it is a review rule.

---

## Decisions made

| Decision               | Choice                                                                                                                              | Status                                                                  |
| ---------------------- | ----------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| Backend                | C# on the newest **.NET LTS** (today .NET 10, supported to November 2028)                                                           | Decided                                                                 |
| Frontend               | **Angular**, newest major, standalone components and signals                                                                        | Decided                                                                 |
| Hosting                | **Serverless on AWS** (Lambda, API Gateway, Cognito, SES, SQS, S3 and CloudFront)                                                   | Decided                                                                 |
| Standards              | The standards of `mortgage-analytics`: Roslyn conventions tests, Stryker, strict lint and format                                    | Decided; extended in documents 1 to 7                                   |
| Scope now              | **`mortgage-analytics` follows the standards first; no existing application is rewritten** ([ADR 0011](adr/0011-mortgage-first.md)) | Decided                                                                 |
| Reuse unit             | **Capability modules**: infrastructure + service + typed client + UI pieces in one versioned unit                                   | Proposed (this document)                                                |
| Deployment             | **One copy per application** by default; a shared running service only where it is naturally shared                                 | Proposed                                                                |
| Repositories           | Few, not one per tiny library; a repository is created when something needs to live in it                                           | Proposed ([document 9](09-repositories-and-delivery.md))                |
| Data store             | PostgreSQL through EF Core by default; DynamoDB for small operational state                                                         | Proposed; the owner should confirm ([ADR 0005](adr/0005-data-store.md)) |
| Infrastructure as code | AWS CDK in **C#**, so infrastructure sits beside the services in one language                                                       | Proposed                                                                |

Two facts were checked on 2026-11-03: AWS announced a managed **`dotnet10`** Lambda runtime on 8 January 2026
([AWS announcement](https://aws.amazon.com/about-aws/whats-new/2026/01/aws-lambda-dot-net-10/)), and Amazon Cognito
supports **passkey sign-in** on the Essentials tier and above
([API reference](https://docs.aws.amazon.com/cognito-user-identity-pools/latest/APIReference/API_WebAuthnConfigurationType.html)).

---

## What we found in the five existing apps

All five live under `C:\Dev`. Sizes and dates come from `git log` and `git ls-files` on 2026-11-03.

| App                                     | Stack                                                        | Tracked files | Last commit |
| --------------------------------------- | ------------------------------------------------------------ | ------------- | ----------- |
| `mortgage-analytics`                    | .NET 10, Angular 22, own cookie and passkey accounts         | 1,146         | 2026-11-02  |
| `GenericWebSite/GenericWebSite` (Hyvva) | .NET 8, Angular 21, Aurora Postgres, Cognito, Stripe Connect | 4,619         | 2026-09-29  |
| `MULTI` (the-hive)                      | .NET 8 and 9, Angular 19, DynamoDB, Cognito, Stripe          | 1,321         | 2026-09-24  |
| `Veranda` (Livenhood, HOA software)     | TypeScript, Next 16, about 15 Lambda services, Postgres RLS  | 1,502         | 2026-04-12  |
| `buybright`                             | TypeScript, React, Lambda, Cognito, Stripe                   | 264           | 2026-05-01  |

What the five have in common, and where the same work was done more than once:

| Duplicated work                                       | Where                                                                                                                                                                      | Evidence                                                                            |
| ----------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| **The same product, twice**                           | Hyvva and the-hive are both multi-tenant small-business site and booking platforms                                                                                         | Both `CLAUDE.md` files open with that description                                   |
| **Mortgage math, twice**                              | buybright `frontend/src/lib/calculations/` (floating point, flat 0.55% PMI, 2025 tax) and `mortgage-analytics` Domain (decimal, about 4,300 tests)                         | `mortgage.ts` versus `src/MortgageAnalytics.Domain/`                                |
| **PII redaction, twice**                              | Hyvva `Hyvva.Api/Logging/PiiPatterns.cs` and the-hive `TheHive.Logging/PiiPatternCatalog.cs`                                                                               | Both 39 lines                                                                       |
| **Rate limiting, three times**                        | Hyvva `RateLimitingServiceCollectionExtensions.cs`, the-hive `DynamoTenantRateLimiter.cs`, this app's `ComputeLimits.cs`                                                   | Different designs for the same need                                                 |
| **Webhook idempotency, twice, inside one repository** | the-hive `services/billing` and `services/booking-site` `DynamoWebhookIdempotencyRepository.cs`                                                                            | Same logic, 64 and 67 lines, differing in comments                                  |
| **Sign-in and Stripe**                                | Cognito in four apps (22 to 188 files each), Stripe in four apps (8 to 414 files each)                                                                                     | Counted with a text search on 2026-11-03                                            |
| **Quality rules**                                     | One type per file, no `DateTime.Now`, three-letter names, a frozen clock, "a status flip is not a tested side effect" appear in several repositories' `CLAUDE.md` or tests | Hyvva `tests/Hyvva.ArchTests`, this app `tests/MortgageAnalytics.Conventions.Tests` |
| **Tooling**                                           | Each repository has its own editor config, Prettier, ESLint, CI and test helpers                                                                                           | Directory listings                                                                  |

Two attempts at sharing already exist, and both stopped at the edge of one repository: the-hive has `platform/shared/*`
(identity, logging, rate limiting, Stripe, feature flags, test support) used by project reference, and Veranda has
`@livenhood/shared`, an npm workspace used by 13 services. Neither is published, so no other app can use them.

The frameworks also drifted apart: .NET 8, 9 and 10; Angular 19, 21 and 22; plus a React and Next track. Shared code cannot
span that spread, which is why this plan fixes one stack.

---

## Library, service, template: the vocabulary

People say "microservice" for all three, so the words are pinned down here.

| Word                  | What it is                                                                   | Runs where                      | Updated by                                                    | Good for                                                              |
| --------------------- | ---------------------------------------------------------------------------- | ------------------------------- | ------------------------------------------------------------- | --------------------------------------------------------------------- |
| **Library**           | Code compiled into an application and shipped with it                        | Inside the app's own process    | Bump the version in each app, redeploy it                     | Logging, redaction, validation helpers, clocks, UI components         |
| **Service**           | A separate program with its own deployment, its own data and a network API   | Its own Lambda or container     | Deploy it once; callers get the change                        | Things that own data or secrets: sending email, billing, a calculator |
| **Template**          | A starting copy of an application, copied once and then owned by the new app | Nowhere; it is a starting point | Not updated; improvements are re-applied by hand or by a tool | The first day of a new app                                            |
| **Capability module** | A bundle of the above for one feature, versioned together (next section)     | Both, see below                 | Bump the version; deploy the app's copy                       | Sign-in, email, billing, audit trail, file storage                    |

Which repository holds the code is a separate question. Libraries and services can each live in their own repository or share one.

**Serverless does not mean microservices.** A Lambda function is a unit of deployment, not a boundary around a business
capability. One service can be five Lambda functions, and one application can have fifty.

---

## The idea: capability modules

A capability module is one feature, built once, with four parts that are versioned together:

```
 capability module "email" version 1.4.0
 +--------------------------------------------------------------------+
 | 1. Infrastructure construct   new EmailCapability(this, "Email", ...)|  CDK in C#: SES identity, queue,
 |                                                                    |  bounce handling, suppression list
 | 2. Service code              the Lambda functions behind it         |  .NET 10
 | 3. Typed client              email.Send(...)  (generated from the   |  NuGet package the app calls
 |                              OpenAPI contract, so it cannot drift)  |
 | 4. UI pieces, if it needs any  unsubscribe page, preferences panel  |  Angular library
 +--------------------------------------------------------------------+
```

An application adds **one line to deploy the capability** and **one line to call it**. The capability's own tests,
logging, validation and documentation come with it, because they are part of the module. How a module is structured is in
[document 8](08-capability-modules.md).

Why a bundle and not a library or a service alone:

- A library alone leaves every app to build the infrastructure, the data and the operations around it again.
- A service alone leaves every app to write its own client, its own error handling and its own UI for it.
- The bundle gives the app the whole feature with a stable contract, and gives us one place to fix a bug.

---

## One shared copy or one copy per app

| Choice                             | Good                                                                                                                                          | Costs                                                                                                          |
| ---------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| **One copy per app** (our default) | Same code, separate deployment and data. One app's bug, bill or outage cannot touch another's. Tenant isolation mistakes stay inside one app. | A fix reaches an app only when that app upgrades and redeploys.                                                |
| **One shared running service**     | Deploy once, every app benefits. Cheaper. Allows one sign-in across apps.                                                                     | Multi-tenancy across apps to design and test. Every app shares its outages. An upgrade hits every app at once. |

We share a running service **only where the thing is naturally shared**: a mortgage calculator that several apps call, or a
single user directory if we decide we want one sign-in across all apps. Everything else is one copy per app.

---

## What we standardize

| Area               | What is standardized                                                                                | Where it lives                          |
| ------------------ | --------------------------------------------------------------------------------------------------- | --------------------------------------- |
| Build rules        | Compiler strictness, analyzers, code style in the build, locked packages, XML docs                  | `platform-standards`                    |
| Conventions tests  | The Roslyn tests that read our code and fail the build when a rule is broken                        | `platform-standards`                    |
| Test stack         | NUnit, NSubstitute, test clock, builders, Testcontainers, Playwright and axe helpers                | `platform-standards` and `platform-kit` |
| Mutation testing   | Stryker settings and score thresholds                                                               | `platform-standards`                    |
| Pipeline           | One reusable workflow, so each app's pipeline file is a few lines                                   | `platform-standards`                    |
| Cross-cutting code | Logging with redaction, request ids, clock, idempotency, rate limiting, security headers, results   | `platform-kit`                          |
| UI                 | Design tokens, components, the app shell (header, account menu, settings, dialogs), theme, language | `platform-ui`                           |
| Capabilities       | Sign-in, email, billing, audit trail, notifications, files, feature flags                           | `platform-capabilities`                 |
| First day          | A starter solution with all of the above wired in                                                   | `app-template`                          |
| Knowledge          | These documents and the primers                                                                     | `platform-standards`                    |

---

## What we do not do

- **No one-repository-per-library.** Many tiny repositories multiply pipelines, version bumps and CI minutes. See [document 9](09-repositories-and-delivery.md).
- **No shared database across apps.** A shared database is a hidden coupling; if two apps need the same data, a service owns it.
- **No capability before it has two real users.** The first extraction is made from a working app; the second app proves it generalizes. Until then it stays in the app (the "rule of three" from [document 1](01-principles.md), relaxed to two for our own platform because we know a second app is coming).
- **No big-bang rewrite.** Existing apps adopt the kit one capability at a time, starting with `mortgage-analytics`.
- **No tool or framework without a primer** ([document 10](10-documentation-standard.md)).

---

## Risks

| Risk                                                                               | What we do about it                                                                                                                    |
| ---------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| A shared package becomes a bottleneck: every change needs a release                | Monorepo for the kits, one command to test and publish; semantic versions; apps upgrade on their own schedule                          |
| The platform grows faster than the apps that use it                                | Two-users rule above; a capability is extracted from a working app, never designed in the abstract                                     |
| Per-app copies drift apart on old versions                                         | A report of which version each app uses; a documented upgrade path per major version; deprecations announced in the changelog          |
| A rule is so strict that people route around it                                    | Every gate has a documented exception process ([document 3](03-quality-gates.md#changing-a-gate)); no silent suppression               |
| The CI-minutes budget is exhausted                                                 | The split of what runs per push, nightly and locally ([document 9](09-repositories-and-delivery.md#the-ci-minutes-budget))             |
| Serverless parts of today's app do not transfer (in-process gate, in-memory cache) | They are redesigned as capabilities (rate limiting at the API Gateway or in DynamoDB, concurrency by reserved concurrency), not copied |
| One person maintains everything                                                    | Everything is documented for a stranger; automation over memory                                                                        |
