# Primers

One short guide per tool or framework we use: what it is, why we chose it, **how it works inside**, how it fits with the others, and what goes
wrong. They are written for someone who has never used the tool. The template and the rules are in the
[documentation standard](../10-documentation-standard.md#primers).

**New here? Read [How everything fits together](00-how-it-fits-together.md) first**, then the primers for the tools you meet.

## Written

Each of these is grounded in code from `mortgage-analytics` (the reference implementation) with the file named, so you can open the real thing.

| Primer                                                                           | Tool                                                                                                 | Read it when you meet...                                               |
| -------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------- |
| [How everything fits together](00-how-it-fits-together.md)                       | (the map)                                                                                            | Day one                                                                |
| [MSBuild and build rules](msbuild-and-build-rules.md)                            | MSBuild, `Directory.Build.props`, central package management, lock files, analyzers, `.editorconfig` | A `.csproj`, a build error, a style rule failing the build             |
| [Dependency injection](dependency-injection.md)                                  | The .NET DI container                                                                                | A constructor with many interfaces, `AddApplication()`, a lifetime bug |
| [NUnit](nunit.md)                                                                | NUnit 5                                                                                              | Any C# test                                                            |
| [NSubstitute](nsubstitute.md)                                                    | NSubstitute                                                                                          | `Substitute.For<...>()`, `.Returns(...)`, `.Received()`                |
| [Testing time](testing-time.md)                                                  | `TimeProvider`, `FakeTimeProvider`                                                                   | Anything with a date, expiry or schedule                               |
| [Roslyn and conventions tests](roslyn-conventions-tests.md)                      | Roslyn (the C# compiler as a library)                                                                | A test that reads source files                                         |
| [NetArchTest and ratchets](netarchtest-and-ratchets.md)                          | NetArchTest, scan tests                                                                              | An architecture or "ban" rule                                          |
| [Stryker (mutation testing)](stryker.md)                                         | Stryker.NET, Stryker with Jest                                                                       | A mutation score, a surviving mutant                                   |
| [Coverage](coverage.md)                                                          | coverlet, Jest coverage, ReportGenerator                                                             | The 85% gate                                                           |
| [WebApplicationFactory](webapplicationfactory.md)                                | ASP.NET Core integration testing                                                                     | `MortgageApiFactory`, an API integration test                          |
| [FluentValidation](fluentvalidation.md)                                          | FluentValidation                                                                                     | A validator, a 400 response                                            |
| [EF Core and Testcontainers](ef-core-and-testcontainers.md)                      | Entity Framework Core, SQLite, PostgreSQL in Docker                                                  | A repository, a migration, a database test                             |
| [OpenAPI and the contract snapshot](openapi-contract-testing.md)                 | Swashbuckle, approved snapshots                                                                      | `ContractSnapshotTests`, "the API contract changed"                    |
| [Jest](jest.md)                                                                  | Jest, jest-preset-angular                                                                            | Any `*.spec.ts`                                                        |
| [Playwright](playwright.md)                                                      | Playwright                                                                                           | Anything in `e2e/`                                                     |
| [axe-core](axe-core.md)                                                          | axe-core, `@axe-core/playwright`                                                                     | An accessibility violation                                             |
| [ESLint, Prettier and stylelint](eslint-prettier-stylelint.md)                   | The three front-end code checkers                                                                    | A lint or format failure                                               |
| [Angular, for our purposes](angular-for-us.md)                                   | Angular (components, signals, DI, routing)                                                           | Any front-end file                                                     |
| [Identity, cookies and Data Protection](identity-cookies-and-data-protection.md) | ASP.NET Core Identity, cookie authentication, Data Protection keys                                   | Sign-in, a session cookie, an emailed link, a key ring                 |
| [Fido2 and passkeys](fido2-passkeys.md)                                          | Fido2 (WebAuthn), passkeys                                                                           | `PasskeyEndpoints`, a passkey ceremony                                 |
| [MailKit and MimeKit](mailkit.md)                                                | MailKit, MimeKit, SMTP                                                                               | `SmtpMailTransport`, an email that does not arrive                     |
| [Health checks](health-checks.md)                                                | ASP.NET Core health checks                                                                           | `/health`, a container that restarts                                   |
| [RxJS](rxjs.md)                                                                  | RxJS 7                                                                                               | `pipe(...)`, `switchMap`, `latestRequest`                              |

## Planned (written when the tool is adopted)

The rule is no tool without a primer, so each of these is written **in the phase that adopts it**, checked against the current official documentation at that
time, not guessed now. Phases are in the [roadmap](../11-roadmap.md).

| Primer                            | Tool                                                                             | Phase |
| --------------------------------- | -------------------------------------------------------------------------------- | ----- |
| AWS CDK in C#                     | AWS CDK, constructs, synth, `cdk diff`                                           | 1     |
| CDK assertions and cdk-nag        | Infrastructure tests                                                             | 1     |
| Lambda and the serverless model   | Lambda, cold starts, native AOT, concurrency, API Gateway                        | 2     |
| Cognito                           | User pools, tokens, passkeys, triggers                                           | 4     |
| SES, SQS and EventBridge          | Email, queues, events                                                            | 4     |
| Stripe                            | Checkout, Connect, webhooks, idempotency keys                                    | 4     |
| OpenTelemetry and X-Ray           | Tracing and metrics                                                              | 2     |
| AWS Lambda Powertools for .NET    | Logging, metrics, tracing, idempotency (evaluated before the kit builds its own) | 2     |
| FsCheck and fast-check            | Property-based testing                                                           | 2     |
| gitleaks and OWASP ZAP            | Secret scanning, dynamic security scan                                           | 1, 4  |
| MinVer and `dotnet new` templates | Versioning from tags; the app template                                           | 1, 2  |
| Typed client generation           | NSwag, Kiota or openapi-typescript (chosen by a spike)                           | 2     |
