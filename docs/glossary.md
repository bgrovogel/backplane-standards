# Glossary

Every term of art, abbreviation and technology name used in these documents, in plain words. If a word on any page is not here and not defined where it first appears, that is a documentation bug.

**Jump to:** [A](#a) · [B](#b) · [C](#c) · [D](#d) · [E](#e) · [F](#f) · [G](#g) · [H](#h) · [I](#i) · [J](#j) · [K](#k) · [L](#l) · [M](#m) · [N](#n) · [O](#o) · [P](#p) · [R](#r) · [S](#s) · [T](#t) · [U](#u) · [V](#v) · [W to Z](#w-to-z)

---

## A

| Term                     | Plain meaning                                                                                                             |
| ------------------------ | ------------------------------------------------------------------------------------------------------------------------- |
| **Accessibility**        | Designing so people can use the product whatever their abilities and tools (screen reader, keyboard only, magnification)  |
| **Accessible name**      | The text a screen reader announces for an element                                                                         |
| **ADR**                  | Architecture decision record: a short file saying what we decided, why, and what else we considered                       |
| **Adapter**              | Code that implements a port for one real technology (a database repository, an SES mail sender)                           |
| **Analyzer**             | A library of rules that inspects code while it compiles and reports warnings or errors                                    |
| **API**                  | Application programming interface: the way one program asks another to do something; here, our HTTP routes                |
| **API Gateway**          | The AWS service that receives HTTP requests and forwards them to Lambda functions, applying limits                        |
| **Application layer**    | The layer holding use cases, contracts, validators, mapping and ports                                                     |
| **Arrange, act, assert** | The three steps of a test: set up, do the one thing, check the result                                                     |
| **Assertion**            | A statement a test requires to be true; if false the test fails                                                           |
| **Audit trail**          | An append-only record of who did what to which record and when                                                            |
| **AWS CDK**              | Cloud Development Kit: a way to describe AWS infrastructure in a programming language and generate CloudFormation from it |

## B

| Term                           | Plain meaning                                                                          |
| ------------------------------ | -------------------------------------------------------------------------------------- |
| **Backward compatible**        | A change that does not break existing users of an interface                            |
| **Boundary value**             | A value at the edge of what is allowed (the minimum, the maximum, and one beyond each) |
| **Branch coverage**            | The share of decision outcomes (the true and false of each `if`) that the tests took   |
| **Bounded context**            | An area of the business with its own language and rules; one feature area              |
| **Budget (size, performance)** | A limit the build or a check enforces, such as the largest allowed first download      |

## C

| Term                            | Plain meaning                                                                                             |
| ------------------------------- | --------------------------------------------------------------------------------------------------------- |
| **Canary (synthetic check)**    | A scheduled script that performs a real journey against the real system to notice it is broken            |
| **Capability module**           | One reusable feature delivered as infrastructure, service, typed client and UI pieces, versioned together |
| **Captive dependency**          | A long-lived object holding a short-lived one it should not outlive                                       |
| **CDK construct**               | A reusable building block of infrastructure in the CDK                                                    |
| **cdk-nag**                     | A tool that checks CDK infrastructure against security and best-practice rules                            |
| **Change detection**            | Angular working out what changed so it can update the screen                                              |
| **CI (continuous integration)** | Automatically building and testing every change on a server                                               |
| **Circuit breaker**             | A guard that stops calling a failing partner for a while, so one outage does not cascade                  |
| **Clean Architecture**          | Organizing code in layers where dependencies point inward toward the business rules                       |
| **CloudFront**                  | AWS's content delivery network: serves our front-end files from locations near the user                   |
| **CloudWatch**                  | AWS's service for logs, metrics and alarms                                                                |
| **Cognito**                     | AWS's sign-in service (user pools, tokens, passkeys)                                                      |
| **Cold start**                  | The extra delay when Lambda starts a new instance of a function                                           |
| **Composition root**            | The single place that wires the application together                                                      |
| **Component (Angular)**         | A class plus a template and styles that renders part of the screen                                        |
| **Constraint model**            | NUnit's style of assertion: `Assert.That(actual, Is.EqualTo(expected))`                                   |
| **Container (DI)**              | The object that builds and supplies the objects a class asks for                                          |
| **Container (Docker)**          | A lightweight, isolated process environment                                                               |
| **Contract**                    | The agreed shape of the JSON a service sends and accepts                                                  |
| **Conventions test**            | A test that reads our source files and fails when a written style rule is broken                          |
| **CORS**                        | Cross-origin resource sharing: browser rules about which websites may call an API                         |
| **Coverage**                    | The share of code executed while the tests ran                                                            |
| **CSRF**                        | Cross-site request forgery: tricking a browser into sending an unwanted authenticated request             |
| **CSP**                         | Content-Security-Policy: a header telling the browser what it may load and run                            |

## D

| Term                          | Plain meaning                                                                        |
| ----------------------------- | ------------------------------------------------------------------------------------ |
| **Dead-letter queue (DLQ)**   | A queue where messages that failed repeatedly are put aside so they are not lost     |
| **`DbContext`**               | EF Core's unit of work: tracks loaded objects and saves changes together             |
| **Definition of done**        | The checklist a change must satisfy before it counts as finished                     |
| **Dependency injection (DI)** | Giving a class what it needs through its constructor instead of letting it create it |
| **Dependency rule**           | Source-code dependencies point only inward, toward the domain                        |
| **Diátaxis**                  | A model separating documentation into tutorials, how-tos, reference and explanation  |
| **DKIM, SPF, DMARC**          | Email authentication standards that prove a message really came from our domain      |
| **Domain layer**              | The layer of business rules and values, with no packages or I/O                      |
| **DRY**                       | Don't repeat yourself: every piece of knowledge has one home                         |
| **DynamoDB**                  | AWS's key-value and document database that scales to zero                            |

## E

| Term                      | Plain meaning                                                                              |
| ------------------------- | ------------------------------------------------------------------------------------------ |
| **EF Core**               | Entity Framework Core: an object-relational mapper for .NET                                |
| **End-to-end (e2e) test** | A test of the whole system through a real browser                                          |
| **Endpoint**              | The code for one route, such as `POST /api/v1/amortization/calculate`                      |
| **Endpoint filter**       | Code that runs before or after an endpoint (validation is one)                             |
| **EventBridge**           | AWS's service for routing events between parts of a system                                 |
| **Expand and contract**   | Making database changes in two releases: add the new thing first, remove the old one later |

## F

| Term                     | Plain meaning                                                             |
| ------------------------ | ------------------------------------------------------------------------- |
| **Fake**                 | A simple but real implementation used in a test (an in-memory repository) |
| **Feature flag**         | A switch that turns a feature on or off without a deploy                  |
| **Fixture (NUnit)**      | A class that groups tests                                                 |
| **Fixture (Playwright)** | Named setup a test asks for and receives                                  |
| **Flaky test**           | A test that sometimes fails without any code change                       |
| **FluentValidation**     | A library for writing validation rules in readable, chained code          |
| **Frozen clock**         | A clock that stays at one moment until a test advances it                 |

## G

| Term             | Plain meaning                                                                  |
| ---------------- | ------------------------------------------------------------------------------ |
| **Gate**         | An automatic check that must pass; the build or merge fails otherwise          |
| **Golden path**  | The well-supported, easy way to do a common task, so people take it by default |
| **Guard clause** | An early `return` or `throw` for a bad case, so the main path is not nested    |

## H

| Term     | Plain meaning                                         |
| -------- | ----------------------------------------------------- |
| **Host** | The program that receives requests and calls our code |
| **HSTS** | A header telling browsers to use HTTPS only           |

## I

| Term                       | Plain meaning                                                                            |
| -------------------------- | ---------------------------------------------------------------------------------------- |
| **IAM**                    | AWS Identity and Access Management: who and what may do which actions on which resources |
| **Idempotent**             | Safe to repeat: doing it twice has the same effect as once                               |
| **Infrastructure as code** | Describing servers, queues and permissions in code that is tested and reviewed           |
| **Infrastructure layer**   | The layer of adapters: database, email, cloud services                                   |
| **Instrumentation**        | Inserting counters into code so its execution can be measured                            |
| **Integration test**       | A test of several parts working together                                                 |
| **Interceptor (Angular)**  | A function that runs on every HTTP request or response                                   |

## J

| Term      | Plain meaning                                                            |
| --------- | ------------------------------------------------------------------------ |
| **Jest**  | The JavaScript test runner we use for front-end tests                    |
| **jsdom** | A JavaScript simulation of a browser's document and window, used by Jest |
| **JWT**   | JSON Web Token: a signed token proving who a caller is                   |

## K

| Term                   | Plain meaning                                                            |
| ---------------------- | ------------------------------------------------------------------------ |
| **KISS**               | Keep it simple                                                           |
| **Kit (platform-kit)** | Our repository of reusable .NET libraries with no data and no deployment |
| **Kestrel**            | ASP.NET Core's built-in web server                                       |

## L

| Term             | Plain meaning                                                               |
| ---------------- | --------------------------------------------------------------------------- |
| **Lambda**       | AWS's service that runs a function on demand and charges only while it runs |
| **Lazy loading** | Downloading a page's code only when it is first needed                      |
| **Linter**       | A tool that flags suspicious or rule-breaking code                          |
| **Lock file**    | A record of the exact package versions installed, so a build is repeatable  |
| **LTS**          | Long-term support: a release supported for several years                    |

## M

| Term                        | Plain meaning                                                                     |
| --------------------------- | --------------------------------------------------------------------------------- |
| **Migration**               | A versioned description of a database schema change                               |
| **Middleware**              | A step every request passes through before and after the endpoint                 |
| **Minimal API**             | ASP.NET Core's lightweight style of declaring HTTP endpoints                      |
| **Mock**                    | A test double that checks it was called as expected                               |
| **Monorepo**                | One repository holding many packages                                              |
| **MSBuild**                 | The build engine behind `dotnet build`                                            |
| **Multi-tenant**            | One running application serving many customers whose data must stay separate      |
| **Mutant / mutation score** | A deliberately changed copy of code / the share of those changes the tests caught |

## N

| Term            | Plain meaning                                                                  |
| --------------- | ------------------------------------------------------------------------------ |
| **NetArchTest** | A library for testing which parts of a .NET codebase depend on which           |
| **NSubstitute** | The library that creates substitutes (mocks and stubs) for interfaces in tests |
| **NuGet / npm** | The package managers for .NET and for JavaScript                               |
| **NUnit**       | The .NET test framework we use                                                 |

## O

| Term              | Plain meaning                                                                    |
| ----------------- | -------------------------------------------------------------------------------- |
| **Observability** | How well you can tell what a running system is doing from the evidence it leaves |
| **OpenAPI**       | A standard format that describes an HTTP API                                     |
| **OpenTelemetry** | A standard for collecting traces, metrics and logs                               |
| **Oracle**        | A second, independent way of computing an answer, used to check the main one     |
| **ORM**           | Object-relational mapper: maps objects to database rows                          |

## P

| Term                    | Plain meaning                                                                             |
| ----------------------- | ----------------------------------------------------------------------------------------- |
| **Passkey**             | A passwordless sign-in using a cryptographic key stored on the person's device (WebAuthn) |
| **PII**                 | Personally identifiable information: data that can identify a person                      |
| **Playwright**          | The tool that drives real browsers for end-to-end tests                                   |
| **Port**                | An interface the application owns, describing something it needs from the outside         |
| **Prettier**            | The code formatter we run over every file                                                 |
| **Primer**              | A short guide to one tool: what, why, how it works inside, how it fits, what goes wrong   |
| **Problem details**     | The standard JSON format (RFC 9457) for HTTP errors                                       |
| **Property-based test** | A test stating a rule that must hold for many generated inputs                            |

## R

| Term                     | Plain meaning                                                               |
| ------------------------ | --------------------------------------------------------------------------- |
| **Ratchet**              | A test that allows a number of violations and only lets the number fall     |
| **RED metrics**          | Rate, errors, duration: the basic health numbers for a service              |
| **Reflection**           | The ability of code to inspect types and call members at run time           |
| **Repository (pattern)** | A class that hides how data is stored behind an interface                   |
| **Roslyn**               | The C# compiler, available as a library                                     |
| **RLS**                  | Row-level security: database rules limiting which rows a connection can see |
| **Runbook**              | A short guide for what to do when a specific alarm fires                    |

## S

| Term                    | Plain meaning                                                                                                           |
| ----------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| **SDK**                 | Software development kit: the toolkit for building with something (.NET SDK, AWS SDK)                                   |
| **Semantic versioning** | Version numbers whose parts say whether a change is a fix, an addition or a break                                       |
| **SES / SQS**           | AWS services for sending email and for message queues                                                                   |
| **Signal**              | An Angular value that notifies its readers when it changes                                                              |
| **SLO**                 | Service level objective: a target such as "99.5% of requests succeed in under a second"                                 |
| **SOLID**               | Five design rules: single responsibility, open/closed, Liskov substitution, interface segregation, dependency inversion |
| **Spy**                 | A test double that records how it was called                                                                            |
| **SSRF**                | Server-side request forgery: tricking a server into fetching an address the attacker chooses                            |
| **STRIDE**              | A checklist of six kinds of security threat                                                                             |
| **Stryker**             | The mutation testing tool                                                                                               |
| **Stub**                | A test double that returns canned answers                                                                               |
| **Substitute**          | NSubstitute's name for a generated test double                                                                          |
| **Syntax tree**         | A program's structure as nested nodes, produced by a parser                                                             |

## T

| Term                        | Plain meaning                                                                       |
| --------------------------- | ----------------------------------------------------------------------------------- |
| **TDD**                     | Test-driven development: write a failing test first, then the code, then clean up   |
| **Template (app-template)** | A starting copy of an application                                                   |
| **Tenant**                  | One customer's isolated world inside a multi-tenant application                     |
| **Testcontainers**          | A library that starts Docker containers (such as PostgreSQL) from test code         |
| **Threat model**            | A written analysis of what could go wrong and how it is defended                    |
| **Tier (of gates)**         | When a check runs: before commit, on every push, on a schedule, or before a release |
| **`TimeProvider`**          | .NET's abstraction over the clock, replaceable in tests                             |
| **TypeScript**              | JavaScript with types                                                               |

## U

| Term            | Plain meaning                                                                     |
| --------------- | --------------------------------------------------------------------------------- |
| **Unit test**   | A test of one small piece of behaviour in isolation                               |
| **Use case**    | One thing the application does for a person, implemented in the Application layer |
| **USE metrics** | Utilization, saturation, errors: the basic numbers for a resource                 |

## V

| Term             | Plain meaning                                                |
| ---------------- | ------------------------------------------------------------ |
| **Validator**    | A class holding the rules that say what a valid request is   |
| **Value object** | A small immutable type defined by its value, such as `Money` |

## W to Z

| Term                      | Plain meaning                                                                  |
| ------------------------- | ------------------------------------------------------------------------------ |
| **WCAG**                  | Web Content Accessibility Guidelines: the international accessibility standard |
| **WebApplicationFactory** | The tool that starts our whole API in memory inside a test                     |
| **Webhook**               | A call another service makes to us when something happens on its side          |
| **WebAuthn**              | The web standard behind passkeys                                               |
| **X-Ray**                 | AWS's service for viewing traces across a request                              |
| **YAGNI**                 | You aren't gonna need it: do not build what is not needed now                  |
| **zone.js**               | A library that tells Angular when asynchronous work finishes                   |
