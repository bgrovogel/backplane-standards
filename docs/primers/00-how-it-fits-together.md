# How everything fits together

> The map. One request from a person's keystroke to the database and back, and one change from an idea to production, naming every tool that
> touches it and the primer that explains it.

**Read time:** 20 min · **Best for:** day one, and whenever a tool makes no sense on its own · **Version:** the stack of
`mortgage-analytics` on 2026-11-03 (.NET 10, Angular 22) with the platform's planned serverless host · **Last verified:** 2026-11-03

**On this page:** [The system in one picture](#the-system-in-one-picture) · [One request, end to end](#one-request-end-to-end) ·
[One change, end to end](#one-change-end-to-end) · [The "magic" explained](#the-magic-explained) · [Where to look for what](#where-to-look-for-what) · [Terms](#terms)

---

## The system in one picture

```
   PERSON
     |  types, clicks
     v
 +------------------------------+            built and checked by:
 |  BROWSER                      |             Angular, TypeScript, signals
 |  Angular app                  |             ESLint, Prettier, stylelint, Jest, Playwright, axe-core
 |   page -> store -> API service|
 +---------------+--------------+
                 |  HTTPS, JSON (the contract)               described by: OpenAPI   checked by: contract snapshot
                 v
 +------------------------------+
 |  EDGE                         |             CloudFront (files), API Gateway (routes, limits)
 +---------------+--------------+
                 v
 +------------------------------+
 |  HOST  (AWS Lambda, .NET)     |             ASP.NET Core Minimal API       checked by: WebApplicationFactory
 |   middleware: request id,     |             logging, security headers, rate limits, auth
 |   validation filter, endpoint |             FluentValidation
 +---------------+--------------+
                 v
 +------------------------------+
 |  APPLICATION  (use cases)     |             mapping, ports (interfaces)    checked by: NUnit + NSubstitute
 +-------+----------------+-----+
         v                v
 +---------------+  +----------------------------+
 |  DOMAIN        |  |  INFRASTRUCTURE (adapters)  |  EF Core -> PostgreSQL, SES, SQS, S3, Cognito
 |  business rules|  |  implements the ports       |  checked by: Testcontainers, port contract suites
 +---------------+  +----------------------------+

 AROUND EVERYTHING:  logs, metrics, traces (OpenTelemetry) | audit trail | alarms and canaries
                     build rules (MSBuild, analyzers) | conventions tests (Roslyn) | architecture tests (NetArchTest)
                     coverage (coverlet) | mutation (Stryker) | docs check | CI (GitHub Actions)
```

Read it from the top: a person acts, the browser calls an API, the host checks and routes, the application decides, the domain
applies the rules, the infrastructure talks to the outside world. Everything in the last block exists to make that path trustworthy.

---

## One request, end to end

The example is the calculator in `mortgage-analytics`: the person changes the interest rate, and a new monthly payment appears.
In the platform the same flow runs through a Lambda host. Each step names the tool and the primer.

| #   | What happens                                                                                                                             | Tool                                    | Primer                                                      | Proved by                                                       |
| --- | ---------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------- | ----------------------------------------------------------- | --------------------------------------------------------------- |
| 1   | The person types `6.5` in the rate field. The form updates a **signal** (a value the framework watches)                                  | Angular                                 | [Angular for us](angular-for-us.md)                         | Jest (component), Playwright (typing)                           |
| 2   | The page hands the new request to a **store**, which waits 400 ms for the typing to pause, and cancels any older request still in flight | Angular, RxJS                           | [Angular for us](angular-for-us.md)                         | Jest (`latest-request.spec.ts`)                                 |
| 3   | The **API service** (the only code that knows a URL) sends `POST /api/v1/amortization/calculate` with the JSON body                      | Angular `HttpClient`                    | [Angular for us](angular-for-us.md)                         | Jest with a fake HTTP backend                                   |
| 4   | The request reaches the host. **Middleware** gives it an id, starts timing, applies rate limits and size limits                          | ASP.NET Core                            | [WebApplicationFactory](webapplicationfactory.md)           | Integration tests (`RequestLoggingTests`, `ComputeLimitsTests`) |
| 5   | The **validation filter** runs the validator for this request type. Invalid input stops here with a 400 and field errors                 | FluentValidation                        | [FluentValidation](fluentvalidation.md)                     | Validator unit tests, boundary tests                            |
| 6   | The **endpoint** calls one use case (an Application service). It contains no business logic                                              | ASP.NET Core Minimal API                | [Dependency injection](dependency-injection.md)             | Integration test of the endpoint                                |
| 7   | The use case **maps** the request to domain objects, asks the **domain** to compute, and maps the result to a response                   | C#                                      | [NUnit](nunit.md), [NSubstitute](nsubstitute.md)            | Application unit tests with ports substituted                   |
| 8   | The **domain** applies the rules (payment formula, PMI, extra payments) with `decimal` money, rounding in one place                      | Pure C#                                 | [NUnit](nunit.md)                                           | Domain unit tests, independent oracle, mutation                 |
| 9   | If data is stored or read, a **repository** (an adapter behind a port) talks to the database                                             | EF Core                                 | [EF Core and Testcontainers](ef-core-and-testcontainers.md) | Repository tests on a real database                             |
| 10  | The response is written as JSON; an error is written as **Problem Details** with the request id                                          | ASP.NET Core                            | [FluentValidation](fluentvalidation.md)                     | Integration tests of error bodies                               |
| 11  | The middleware writes **one log line** (method, path, status, milliseconds, request id), and updates metrics                             | `ILogger`, `System.Diagnostics.Metrics` | [Logging and observability](../06-logging-observability.md) | `RequestLoggingTests`                                           |
| 12  | The browser receives the JSON, the store updates a signal, and the page shows the new payment                                            | Angular                                 | [Angular for us](angular-for-us.md)                         | Playwright journey, axe check                                   |

Two rules to notice: **validation happens once, at step 5**, so steps 7 and 8 can trust their input; and **each step is tested at the lowest level that can prove it**, so the
domain's tests need no web server and the web tests need no domain detail.

---

## One change, end to end

The path of a change, from "we want the app to do X" to "it is running in production". Every row is a gate or a step; the IDs are in
[Quality gates](../03-quality-gates.md).

| #   | Step                                                                                                                              | Tool                                       | Primer                                                                              | Gate          |
| --- | --------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------ | ----------------------------------------------------------------------------------- | ------------- |
| 1   | Decide the expected behaviour from an **independent** source (a hand calculation, a regulator's example, a second implementation) | Python models, reference data              | [Testing strategy](../04-testing-strategy.md#detail-the-kinds-that-surprise-people) | G-33 (oracle) |
| 2   | Write a **failing test** that pins it, and watch it fail for the right reason                                                     | NUnit (C#) or Jest (TypeScript)            | [NUnit](nunit.md), [Jest](jest.md)                                                  | TDD           |
| 3   | Write the smallest code that passes; refactor with the tests green                                                                | C#, TypeScript                             |                                                                                     |               |
| 4   | **The build runs** and rejects warnings, unused code, bad formatting, missing XML docs                                            | MSBuild, Roslyn analyzers, `.editorconfig` | [MSBuild and build rules](msbuild-and-build-rules.md)                               | G-01 to G-05  |
| 5   | **Conventions tests** read the source and fail on a broken style rule                                                             | Roslyn                                     | [Roslyn and conventions](roslyn-conventions-tests.md)                               | G-08          |
| 6   | **Architecture tests** check the layers and banned constructs                                                                     | NetArchTest, scan tests                    | [NetArchTest and ratchets](netarchtest-and-ratchets.md)                             | G-09          |
| 7   | **Unit tests** run, with ports substituted and time frozen                                                                        | NUnit, NSubstitute, FakeTimeProvider       | [NSubstitute](nsubstitute.md), [Testing time](testing-time.md)                      | G-20          |
| 8   | **Integration tests** run the real pipeline in memory against a real database                                                     | WebApplicationFactory, EF Core, SQLite     | [WebApplicationFactory](webapplicationfactory.md)                                   | G-20, G-24    |
| 9   | The **contract snapshot** compares the API's JSON shape with the approved file                                                    | Swashbuckle, an approved-file test         | [OpenAPI snapshot](openapi-contract-testing.md)                                     | G-23          |
| 10  | **Coverage** is measured and must be at least 85%                                                                                 | coverlet, Jest, ReportGenerator            | [Coverage](coverage.md)                                                             | G-21          |
| 11  | **Mutation testing** changes the code and checks the tests notice (nightly or before release)                                     | Stryker                                    | [Stryker](stryker.md)                                                               | G-22          |
| 12  | **Front-end checks:** lint, format, typecheck, unit tests                                                                         | ESLint, Prettier, stylelint, `tsc`, Jest   | [ESLint, Prettier, stylelint](eslint-prettier-stylelint.md), [Jest](jest.md)        | G-05 to G-07  |
| 13  | **End-to-end tests** drive a real browser through the journeys on the production build                                            | Playwright                                 | [Playwright](playwright.md)                                                         | G-25          |
| 14  | **Accessibility** is scanned on every page, theme and language                                                                    | axe-core with Playwright                   | [axe-core](axe-core.md)                                                             | G-26          |
| 15  | The **docs check** confirms counts, links and citations still match                                                               | A Python script                            | [Documentation standard](../10-documentation-standard.md)                           | G-14          |
| 16  | **Commit** (one idea, a message that says why); **push**; CI repeats tier 2                                                       | Git, GitHub Actions                        | [Repositories and delivery](../09-repositories-and-delivery.md)                     | tier 2        |
| 17  | **Release** a library or **deploy** an app (the owner does this), after reading `cdk diff`                                        | AWS CDK                                    | CDK primer (planned)                                                                | deploy rules  |
| 18  | A **canary** and the **alarms** watch the real system                                                                             | CloudWatch, a synthetic check              | [Logging and observability](../06-logging-observability.md)                         | G-42, G-43    |

---

## The "magic" explained

Some things in this stack look like magic until the mechanism is known. Each is explained properly in its primer; here is the one-paragraph version.

| It looks like...                                                          | What is really happening                                                                                                                                             | Primer                                                |
| ------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------- |
| A constructor that "just receives" its collaborators                      | A container reads the constructor's parameter types and builds the objects for you; you told it what to build in one place (the composition root)                    | [Dependency injection](dependency-injection.md)       |
| `Substitute.For<IMailTransport>()` produces a working fake out of nothing | At run time a library generates a new class that implements the interface and routes every call to a recorder. `Returns(...)` configures the recorder                | [NSubstitute](nsubstitute.md)                         |
| A test method that "runs by itself"                                       | The runner loads the compiled test assembly, uses reflection to find every method with `[Test]`, creates the class, and calls the method; nothing is invoked by name | [NUnit](nunit.md)                                     |
| `WebApplicationFactory` starts "the whole API" in a test                  | It runs the application's real startup code in the same process, but replaces the network with an in-memory pipe, so requests never leave the process                | [WebApplicationFactory](webapplicationfactory.md)     |
| A test that fails because of a missing `var` or a short name              | The test never runs your code: it parses the _source files_ with the compiler library and inspects the syntax tree                                                   | [Roslyn and conventions](roslyn-conventions-tests.md) |
| "Mutation score 96%"                                                      | A tool made hundreds of small deliberate bugs in a copy of the code, one at a time, and counted how many your tests caught                                           | [Stryker](stryker.md)                                 |
| A coverage percentage                                                     | The compiled code was rewritten to count each line and branch it executed during the tests; the counts were turned into a report                                     | [Coverage](coverage.md)                               |
| A browser test that "waits for the right moment"                          | Before each action the tool checks the element is visible, stable and enabled, retrying until it is or time runs out; no fixed sleeps                                | [Playwright](playwright.md)                           |
| An accessibility scan that "knows" a page is inaccessible                 | A script computes each element's role, name and colours from the live page and applies a rule library to them                                                        | [axe-core](axe-core.md)                               |
| A reactive page that "updates itself"                                     | Signals track which computations read them; changing a signal notifies exactly those computations                                                                    | [Angular for us](angular-for-us.md)                   |
| A validator that "knows" which field failed                               | Rules are written with expressions (`request => request.Rate`); the library reads the expression to learn the property name                                          | [FluentValidation](fluentvalidation.md)               |
| A build that fails over formatting                                        | The compiler runs style analyzers during the build and the `.editorconfig` raises their severity to error                                                            | [MSBuild and build rules](msbuild-and-build-rules.md) |

---

## Where to look for what

| You want to...                       | Look in...                                                                                              |
| ------------------------------------ | ------------------------------------------------------------------------------------------------------- |
| Know where a piece of code should go | [Architecture](../02-architecture.md#where-does-this-go-backend)                                        |
| Know what must pass before merging   | [Quality gates](../03-quality-gates.md)                                                                 |
| Choose which test to write           | [Testing strategy](../04-testing-strategy.md#which-test-do-i-write)                                     |
| Add a field or an endpoint           | [Validation and errors](../05-validation-and-errors.md#checklist-for-a-new-field)                       |
| Diagnose a production problem        | [Logging and observability](../06-logging-observability.md#finding-a-production-problem-a-walk-through) |
| Review something for security        | [Security baseline](../07-security-baseline.md#review-checklist-for-a-capability)                       |
| Use or build a reusable feature      | [Capability modules](../08-capability-modules.md)                                                       |
| Release a package                    | [Repositories and delivery](../09-repositories-and-delivery.md#versioning-and-releases)                 |
| Learn a tool                         | The [primer index](README.md)                                                                           |
| Understand a decision                | [ADRs](../adr/README.md)                                                                                |

---

## Terms

| Term               | Plain meaning                                                                                            |
| ------------------ | -------------------------------------------------------------------------------------------------------- |
| **Host**           | The program that receives requests and calls our code (here ASP.NET Core running on Lambda)              |
| **Middleware**     | A step every request passes through before and after the endpoint (logging, limits, authentication)      |
| **Endpoint**       | The code for one route, such as `POST /api/v1/amortization/calculate`                                    |
| **Use case**       | One thing the application does for a person, implemented as a method in the Application layer            |
| **Port / adapter** | An interface the application owns (port) and the code that implements it for a real technology (adapter) |
| **Contract**       | The agreed shape of the JSON a service sends and accepts                                                 |
| **Signal**         | An Angular value that tells whatever reads it when it changes                                            |
| **Gate**           | An automatic check that must pass; the build fails otherwise                                             |
| **Tier**           | When a gate runs: before commit, on every push, on a schedule, or before a release                       |
| **Oracle**         | A second, independent way of computing an answer, used to check the main one                             |

The full list is the [glossary](../glossary.md).
