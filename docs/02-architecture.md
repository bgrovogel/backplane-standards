# 2. Architecture

> Every kind of code has one obvious home. A stranger should be able to answer "where does this live?" from the folder
> names alone, and a build should fail when something is put in the wrong place.

**Read time:** 20 min · **Best for:** where to put a new piece of code, and how to find an old one · **Builds on:**
[Principles](01-principles.md) · **Reference implementation:** `mortgage-analytics` (its own write-up is
`docs/03-architecture.md` and `docs/04-backend.md` in that repository)

**On this page:** [Three scales](#three-scales) · [The dependency rule](#the-dependency-rule) ·
[The backend](#the-backend) · [Where does this go? (backend)](#where-does-this-go-backend) ·
[Running on Lambda](#running-on-lambda) · [The frontend](#the-frontend) ·
[Where does this go? (frontend)](#where-does-this-go-frontend) · [Across repositories](#across-repositories) ·
[Making things findable](#making-things-findable) · [Naming](#naming) · [How each rule is enforced](#how-each-rule-is-enforced) ·
[Where today's app bends the rules](#where-todays-app-bends-the-rules)

---

## Three scales

"Architecture" answers the same question at three sizes. Each has its own map.

| Scale                   | The question                                                             | The map                                     |
| ----------------------- | ------------------------------------------------------------------------ | ------------------------------------------- |
| **Across repositories** | Which repository holds a standard, a kit, a capability, a starter?       | [Across repositories](#across-repositories) |
| **Inside one backend**  | Which project and folder holds a rule, a use case, a query, an endpoint? | [The backend](#the-backend)                 |
| **Inside the frontend** | Which folder holds a page, a shared widget, an API call, a store?        | [The frontend](#the-frontend)               |

One idea runs through all three, and it is the one to remember:

> **Dependencies point toward the part that changes least.** Business rules change least and depend on nothing.
> Frameworks, databases, cloud services and screens change most and depend on the rules, never the other way round.

---

## The dependency rule

Clean Architecture (Robert C. Martin) draws the system as rings. The inner rings hold what the business cares about; the outer
rings hold tools. Code may only depend inward.

```
            +----------------------------------------------------+
            |  Api / Host    HTTP endpoints, Lambda entry, startup |   outermost: changes with frameworks
            |  +------------------------------------------------+ |
            |  | Infrastructure   database, email, AWS, files     | |   adapters: implement the ports
            |  |  +--------------------------------------------+ | |
            |  |  | Application   use cases, contracts, ports,  | | |   what the system DOES
            |  |  |               validation, mapping            | | |
            |  |  |  +----------------------------------------+ | | |
            |  |  |  | Domain   business rules and values      | | | |   what the business IS
            |  |  |  |          no packages, no I/O            | | | |
            |  |  |  +----------------------------------------+ | | |
            |  |  +--------------------------------------------+ | |
            |  +------------------------------------------------+ |
            +----------------------------------------------------+

            Allowed:  Api -> Application -> Domain          Infrastructure -> Application -> Domain
            Forbidden: Domain -> anything else,  Application -> Infrastructure,  Application -> Api
```

The key device is the **port**: the Application layer declares an interface for what it needs ("save this calculation",
"send this email"), and the Infrastructure layer provides the **adapter** that implements it. The use case never knows
whether the adapter talks to PostgreSQL, DynamoDB or an in-memory fake. This is the Dependency Inversion Principle
([document 1](01-principles.md#solid)).

Why we pay for this:

- The business rules are tested in milliseconds with no setup, because nothing in them does I/O.
- A database, a cloud service or a framework can be replaced by writing one adapter.
- The public contract (the JSON) cannot change by accident, because it has its own types, separate from the domain.

---

## The backend

Every application's backend has the same four projects, named `<App>.<Layer>`.

| Project                | Holds                                                                                                 | May reference                                     | Never contains                                                       |
| ---------------------- | ----------------------------------------------------------------------------------------------------- | ------------------------------------------------- | -------------------------------------------------------------------- |
| `<App>.Domain`         | Entities, value objects, business rules, domain services, domain exceptions. **No NuGet packages.**   | nothing                                           | Database types, HTTP types, JSON attributes, logging, `DateTime.Now` |
| `<App>.Application`    | Use cases (services), request and response **contracts**, validators, mappers, **ports** (interfaces) | Domain, FluentValidation                          | EF Core, ASP.NET, AWS SDK, domain types in contracts                 |
| `<App>.Infrastructure` | Adapters: EF Core context and repositories, migrations, email and storage clients, AWS SDK calls      | Application                                       | Business rules, HTTP concerns                                        |
| `<App>.Api`            | The host: endpoints, filters, middleware, authentication wiring, `Program.cs` (the composition root)  | Application, Infrastructure (to register it only) | Business rules, queries, mapping logic                               |

Tests mirror this: `<App>.Domain.Tests`, `<App>.Application.Tests`, `<App>.Infrastructure.Tests`,
`<App>.Api.IntegrationTests`, plus `<App>.Conventions.Tests`, `<App>.Architecture.Tests` and `<App>.Testing` (clock, builders,
reference data). See [Testing strategy](04-testing-strategy.md).

### The standard solution layout

```
<App>/
  global.json  Directory.Build.props  Directory.Packages.props  .editorconfig     one set of rules for every project
  src/
    <App>.Domain/
      <BusinessConcept>/            Loans/, Refinancing/, Booking/  - folders are named for the business, not the technology
    <App>.Application/
      Contracts/                    request and response types: the public shape of the API
      Validation/                   one validator per request type
      Mapping/                      contract <-> domain
      <Feature>/                    Passkeys/, Sessions/, SavedCalculations/ - a use case, its port, its data
      Services/                     use-case services that span features
      DependencyInjection.cs        AddApplication()
    <App>.Infrastructure/
      Migrations/  <Adapter>.cs     CalculationsDbContext, SavedCalculationRepository, ...
      DependencyInjection.cs        AddInfrastructure()
    <App>.Api/
      Endpoints/                    one file per feature area, thin: validate, call a use case, shape the HTTP answer
      Filters/  Security/  Observability/  Errors/
      Program.cs                    the only file that knows every layer
  tests/
    <App>.Domain.Tests/  <App>.Application.Tests/  <App>.Infrastructure.Tests/
    <App>.Api.IntegrationTests/  <App>.Conventions.Tests/  <App>.Architecture.Tests/  <App>.Testing/
  client/                           the Angular application (next sections)
  infra/                            CDK in C#: the stacks that deploy this app
  docs/  tools/                     documentation and independent checks
```

Three habits make this layout findable:

1. **Folders inside a layer are named for the business concept** (`Loans`, `Refinancing`, `ExtraPayments`), never for the kind of
   class (`Interfaces`, `Services`, `Models`). Open `Domain/` and you read what the business is about. This is sometimes
   called "screaming architecture": the structure shouts its purpose.
2. **A feature appears in the same place in each layer.** `Passkeys` has a folder in Application (use case and port), a repository
   in Infrastructure and an endpoint file in Api. Searching for the feature name finds every part.
3. **The composition root is the only place that knows everything.** `Program.cs` calls `AddApplication()` and
   `AddInfrastructure()` and maps the endpoints. Nothing else decides which adapter is used.

### The rules of each layer, in detail

**Domain.** Plain C# objects that enforce their own rules. Money is `decimal` and rounds in one named place
([ADR 0007](adr/0007-money.md)). Invalid states are unrepresentable where practical: a loan with a negative term cannot be
constructed, it throws a domain exception. The Domain takes the current time from a `TimeProvider` passed in, never from `DateTime.Now`.

**Application.** One class per use case or one cohesive service per feature. It receives a contract, validates it, maps it
to domain types, calls the domain, maps the result to a response contract. It declares ports. It owns the contract
types, so renaming a domain class can never change the JSON; a test fails if a contract exposes a domain type (that test
exists today in `mortgage-analytics`: `ContractLayeringTests`).

**Infrastructure.** Everything that touches the outside world, one adapter per concern. An adapter has no business rules:
if you find an `if` that a product owner would recognise, it belongs in the Domain or the Application layer.

**Api.** A thin shell. An endpoint does four things: authenticate and authorize, validate (a filter does this), call one
use case, translate the result to an HTTP status. If an endpoint is longer than a screen, logic is leaking into it.

---

## Where does this go? (backend)

| You are writing...                                                    | It goes in...                                                                           | Because                                                                                 |
| --------------------------------------------------------------------- | --------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| A business rule ("PMI ends at 78%")                                   | `Domain/<Concept>/`                                                                     | It is true whatever the technology                                                      |
| A value that must always be valid (`Money`, `EmailAddress`)           | `Domain/`                                                                               | The type guarantees its own validity                                                    |
| A request or response shape                                           | `Application/Contracts/`                                                                | It is the public API, owned by the Application                                          |
| A check on input ("rate between 0 and 30")                            | `Application/Validation/`, one validator per request                                    | Validated at the boundary, before any logic ([document 5](05-validation-and-errors.md)) |
| Turning a request into domain objects, or the reverse                 | `Application/Mapping/`                                                                  | Keeps both sides ignorant of each other                                                 |
| "Save this", "find that", "send this" (the need)                      | An interface in `Application/<Feature>/`                                                | The use case states what it needs                                                       |
| "Save this" implemented on PostgreSQL                                 | `Infrastructure/`                                                                       | An adapter                                                                              |
| A database column, index or migration                                 | `Infrastructure/` (entity configuration, `Migrations/`)                                 | Persistence detail                                                                      |
| Calling AWS (SES, S3, Cognito, SQS)                                   | `Infrastructure/`, behind a port                                                        | The cloud SDK never reaches the Application layer                                       |
| A URL, an HTTP status, a header, a cookie                             | `Api/`                                                                                  | Transport detail                                                                        |
| Authentication and authorization wiring                               | `Api/Security/`, with the policy names in a shared constant                             | One place to audit                                                                      |
| A cross-cutting concern (request logging, error mapping, rate limits) | Middleware or a filter in `Api/`, from the kit ([document 8](08-capability-modules.md)) | Applied once to every request                                                           |
| Configuration values                                                  | An options class bound from configuration, validated at startup                         | Typed, discoverable, fails fast                                                         |
| A secret                                                              | A secret store (AWS Secrets Manager); never a file in the repository                    | [Security baseline](07-security-baseline.md)                                            |
| A feature that several apps need                                      | A capability module, not a copy                                                         | [Capability modules](08-capability-modules.md)                                          |
| A "helper" with nowhere obvious to go                                 | Stop. Find the concept it belongs to, or make that concept a type                       | There are no `Utils` folders                                                            |

---

## Running on Lambda

Serverless changes how the host is packaged, not the layers. The Domain, Application and Infrastructure projects are the same;
only `<App>.Api` differs by how it is started. The open question is how many Lambda functions one application becomes.

| Option                                       | Good                                                                                                                | Costs                                                                         |
| -------------------------------------------- | ------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------- |
| One function per route (the-hive does this)  | Smallest blast radius, finest permissions                                                                           | Many artifacts and cold starts; shared code repeated in each; slow local runs |
| **One function per feature area** (proposed) | A few artifacts; shared middleware and connection pool inside each; the same Minimal API runs locally and on Lambda | A function holds several routes, so its permissions are the union             |
| One function for the whole app               | Simplest                                                                                                            | Biggest cold start; no isolation between features                             |

The proposal is recorded as [ADR 0006](adr/0006-lambda-granularity.md): **one function per feature area (bounded context),
each a Minimal API.** Hyvva runs ASP.NET Core Minimal APIs with native AOT on Lambda, so the pattern is proven in this codebase
family. Whatever is chosen, the rule for permissions is the same: a function gets only the permissions its own area needs
([Security baseline](07-security-baseline.md)).

Serverless also removes assumptions today's app makes. A per-process in-memory gate or cache is meaningless when each request can
land on a different instance, so those become capabilities built for it (rate limiting in API Gateway or DynamoDB; a cache in
DynamoDB or none).

---

## The frontend

The Angular application has three top-level folders under `src/app`, and a strict direction of dependency between them.

```
client/src/app/
  core/        the application's plumbing: one instance of each, used everywhere
    auth/  i18n/  layout/  errors/  forms/  formatting/  state/  services/  models/ ...
  shared/      reusable pieces that more than one feature uses (components, directives, pipes)
  features/    one folder per screen area; each is a lazy-loaded route
    account/  calculator/  history/  settings/ ...
```

```
   features/<a>      features/<b>         features never import each other
        \               /
         v             v
        shared/   (pieces more than one feature uses)
            \
             v
           core/   (singletons and plumbing; imports nothing above it)
```

The rules:

1. **Dependencies run one way:** `features` may import `shared` and `core`; `shared` may import `core`; `core` imports neither.
2. **A feature never imports another feature.** If two features need the same thing, it moves down into `shared/`. This
   is what keeps features removable and lets each load lazily.
3. **Components never call `HttpClient`.** They call a typed API service in `core/services/`, which is the only code that knows
   a URL. The API service takes and returns the generated contract types ([document 8](08-capability-modules.md)).
4. **State lives in stores, not components.** A store holds signals and exposes read-only views; components read them and call
   methods. Anything that should survive a page change belongs in a store (`core/state/`).
5. **Pages are smart, widgets are plain.** A page component (in `features/`) wires stores and routes together. A shared widget
   takes inputs, emits outputs, and knows nothing about stores or the router.
6. **One component per file, with its template, style and spec beside it:** `history-page.component.ts`,
   `.html`, `.scss`, `.spec.ts`. This is already how `mortgage-analytics` is laid out.
7. **Pure logic is a plain function in its own file**, tested without Angular (`core/formatting/duration.ts`), not buried in a
   component.
8. **Routes are lazy** by default; a feature's route file is the only way into it.
9. **Every user-visible string goes through the translation function**, and a test fails when a language is missing a message.
10. **Every page has loading, empty and error states.** (Hyvva's tripwire list says the same.)

---

## Where does this go? (frontend)

| You are writing...                                         | It goes in...                                                 |
| ---------------------------------------------------------- | ------------------------------------------------------------- |
| A new screen                                               | `features/<name>/` with its route file                        |
| A widget used by two or more features                      | `shared/<name>/`                                              |
| A call to the backend                                      | `core/services/<thing>-api.service.ts`, using generated types |
| State that survives navigation, or is shared by components | `core/state/<thing>-store.ts`                                 |
| A route guard or an HTTP interceptor                       | `core/auth/` or `core/errors/`                                |
| Formatting, a calculation, a parser                        | A pure function in `core/formatting/` or beside its only user |
| A design token (colour, spacing, type size)                | The UI kit's token file, never a literal in a component       |
| A translation                                              | `core/i18n/<language>/`, one file per area                    |
| An end-to-end test                                         | `e2e/<area>.spec.ts`, using a page-object file for the screen |

---

## Across repositories

At the largest scale the same rule applies: reusable, stable things in the middle; applications on the outside depending on them.

```
   applications  (one repository each)  --->  backplane-capabilities  (login, email, billing, ...)
        |                                          |
        |                                          v
        +----------------------------------->  backplane-kit (.NET libraries)   backplane-ui (Angular libraries)
        |                                          |
        v                                          v
   app-template  (the starting copy)  ------>  backplane-standards  (rules, conventions tests, pipeline, docs)
```

| Repository               | Holds                                                                                                                                 | Depends on                       |
| ------------------------ | ------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------- |
| `backplane-standards`    | Build props, analyzers, `.editorconfig`, conventions tests, lint and format presets, Stryker base, reusable pipeline, these documents | nothing                          |
| `backplane-kit`          | .NET libraries: logging and redaction, request ids, clock, idempotency, rate limiting, security headers, results, test support        | standards                        |
| `backplane-ui`           | Angular libraries: design tokens, components, app shell, i18n, theme                                                                  | standards                        |
| `backplane-capabilities` | Capability modules: infrastructure, service, client and UI pieces per feature                                                         | kit, ui, standards               |
| `app-template`           | A starter solution wired to all of the above                                                                                          | all of the above (it is a copy)  |
| each application         | Its own domain, screens and configuration                                                                                             | capabilities, kit, ui, standards |

A repository is created when something needs to live in it, not before ([document 9](09-repositories-and-delivery.md)). The
layout inside a capability is in [document 8](08-capability-modules.md#anatomy-of-a-capability).

---

## Making things findable

Structure alone is not enough; a newcomer needs signposts.

1. **Every top-level folder, and every project, has a `README.md`** of five lines: what lives here, what does not, who may
   depend on it, where to read more. A test fails when one is missing.
2. **Each repository has a generated `MAP.md`** listing every project and top-level folder with a one-line purpose, regenerated by a
   script and checked for staleness by the documentation check ([document 10](10-documentation-standard.md)). Veranda and Hyvva
   already keep a `REPO_MAP.md`; this makes it a rule.
3. **The 10-second test.** Pick any piece of behaviour; a stranger must find the code that implements it from folder names
   alone. If they cannot, the structure is wrong, not the stranger.
4. **No dumping grounds.** No folders or classes named `Utils`, `Helpers`, `Common`, `Misc`, `Manager` or `Base` without a business
   noun in front. A conventions test fails on them.
5. **Folder equals namespace.** The namespace of a C# type is its folder path, so the type name leads you to the file.
6. **One type per file, named after the type** (a rule that already exists and is enforced).
7. **Documentation links to code by path and line, and a check verifies the lines**, so the map never silently rots.

---

## Naming

| Thing                   | Convention                                                   | Example                                                      |
| ----------------------- | ------------------------------------------------------------ | ------------------------------------------------------------ |
| C# project              | `<App>.<Layer>`; capability: `<Brand>.<Capability>.<Part>`   | `Acme.Application`, `Brand.Email.Client`                     |
| C# type                 | A noun that says what it is, never `Data`, `Info`, `Manager` | `LumpSumExtraPayment`, `PaymentFormula`                      |
| Interface               | `I` plus the role                                            | `ISavedCalculationRepository`                                |
| Test class              | `<Subject>Tests`                                             | `AdjustableRateTests`                                        |
| Test method             | `Subject_Condition_Outcome`, reads as a sentence             | `AddingAPasskey_WhileTheMailServiceIsDown_StillAddsIt`       |
| Local, parameter, field | At least three letters, named for what it holds              | `payment => payment.Amount`, not `p => p.Amount` (enforced)  |
| Angular file            | `<name>.<kind>.ts`                                           | `history-page.component.ts`, `amortization-api.service.ts`   |
| Branches and commits    | Conventional style, one idea per commit                      | See [quality gates](03-quality-gates.md#commits-and-reviews) |

---

## How each rule is enforced

| Rule                                                                                          | Mechanism                                                                                | Status                                                                                                                   |
| --------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| Domain depends on nothing; Application never on Infrastructure or Api                         | **Project references** (the compiler refuses) plus an architecture test with NetArchTest | References: exist. Layer test: **packaged** (`LayerRule`)                                                                |
| Contracts never expose domain types                                                           | `ContractLayeringTests` (reflection)                                                     | Exists in `mortgage-analytics`; to package (task 24)                                                                     |
| Core has no AWS SDK dependency                                                                | Architecture test (`CoreHasNoAwsSdkDependencyTests` in Hyvva)                            | **Packaged**: `Layer.AwsSdk` in `LayerRule` (package and `using`)                                                        |
| One type per file; no nested classes; names of three letters; `var` only where the type shows | Roslyn conventions tests                                                                 | **Packaged** in `Backplane.Conventions`                                                                                  |
| No `DateTime.Now`, no `dynamic`, no `console.log`, no `any`, money never `float`              | "Ratchet" architecture tests (a count that may only fall to zero)                        | **Packaged**: `LocalTimeRule`, `DynamicRule`, `FloatingPointRule`, `ComplexityRule`. `console.log` and `any` are not yet |
| Features never import features; components never call `HttpClient`                            | An ESLint import rule (works in the editor); a scan test as a backstop                   | Features never import features: **packaged** (`frontend-presets`). `HttpClient` rule: task 26                            |
| A folder, project or repository has a README                                                  | Documentation check                                                                      | **Packaged**: `ReadmeRule` and the documentation check                                                                   |
| No `Utils`, `Helpers`, `Common`, `Manager` names                                              | Roslyn conventions test                                                                  | **Packaged** (`VagueNamesRule`)                                                                                          |
| Every page has loading, empty and error states                                                | Playwright test per page, listed in a coverage check                                     | Partly in `mortgage-analytics` (error states not covered everywhere)                                                     |
| Each architecture rule is itself tested (the test finds files, the rule can fail)             | A "scanner is alive" test beside every scan, like `SourceFiles_AreFound`                 | Exists                                                                                                                   |

A **ratchet** deserves a sentence: it is a test that counts violations of a rule and fails if the count rises. A rule we cannot
fix everywhere yet starts as "at most 40", and only ever tightens to zero. Hyvva uses the pattern widely (mostly to lock in a zero
already reached) and it is the right tool for adopting a new rule on an existing codebase without a flag day.

---

## Where today's app bends the rules

Honesty about the reference implementation, so nobody copies a shortcut:

- `mortgage-analytics` keeps **accounts, email and the rates provider inside the `Api` project** (`Api/Email`, `Api/Rates`,
  `Api/Security`). They behave like Infrastructure and Application pieces. In the platform they become the **login** and
  **email** capabilities, which is the point of this plan.
- Its **in-process compute gate and in-memory cache** assume one long-running server ([document 0](00-strategy.md#risks)).
- Its frontend has **no import-boundary rule**; the "features never import features" rule is followed by habit. We add the rule
  before the first application copies the habit.
- Hyvva, the-hive and `mortgage-analytics` differ on money: Hyvva stores integer cents, `mortgage-analytics` uses `decimal`.
  [ADR 0007](adr/0007-money.md) picks one.
