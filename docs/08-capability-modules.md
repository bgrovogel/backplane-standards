# 8. Capability modules

> A reusable feature (sign-in, email, billing) built once as a versioned bundle: infrastructure, service, typed client and UI
> pieces, with its own tests, fakes, documentation and runbooks. A new application adds it in a few lines.

**Read time:** 20 min · **Best for:** building a capability, or using one in an app · **Builds on:**
[Strategy](00-strategy.md#the-idea-capability-modules), [Architecture](02-architecture.md) · **Brand name:** the platform has no name yet;
`<Brand>` below stands for it ([roadmap, open decisions](11-roadmap.md#open-decisions))

**On this page:** [What a capability is](#what-a-capability-is) · [Anatomy of a capability](#anatomy-of-a-capability) ·
[The contract](#the-contract) · [Using a capability in an app](#using-a-capability-in-an-app) · [How the service code is packaged](#how-the-service-code-is-packaged) ·
[Rules for a capability](#rules-for-a-capability) · [Versioning](#versioning) · [Talking to a capability](#talking-to-a-capability) ·
[What every capability documents](#what-every-capability-documents) · [Candidates and where they come from](#candidates-and-where-they-come-from) ·
[How a capability is born](#how-a-capability-is-born) · [Mistakes to avoid](#mistakes-to-avoid)

---

## What a capability is

A **capability** is one business-agnostic feature that several applications need, delivered as one versioned unit with four parts:

| Part                         | What it is                                                                                     | Packaged as                                               |
| ---------------------------- | ---------------------------------------------------------------------------------------------- | --------------------------------------------------------- |
| **Infrastructure construct** | AWS CDK code that creates everything it needs (functions, queues, tables, permissions, alarms) | A NuGet package (C#, `Amazon.CDK.Lib`)                    |
| **Service**                  | The code that runs: Lambda handlers using the Clean Architecture layers                        | A prebuilt, versioned deployment artifact (see below)     |
| **Typed client**             | What an application calls (`email.SendAsync(...)`); generated from the contract                | A NuGet package                                           |
| **UI pieces** (optional)     | Angular components and routes the feature needs (a sign-in form, a preferences panel)          | An npm package in `platform-ui` or the capability's `ui/` |

A capability **owns its own data**. No application reads its tables; applications use the client, or subscribe to its events.

A capability is **not** a library (it runs, has data and an operations story) and **not necessarily a shared service** (by default each application
deploys its own copy, [strategy](00-strategy.md#one-shared-copy-or-one-copy-per-app)).

---

## Anatomy of a capability

One folder per capability inside the `platform-capabilities` repository. The names below use `email` as the example.

```
capabilities/email/
  README.md                      what it is, in five lines; links to the rest
  contract/
    openapi.yaml                 the HTTP contract: the source of truth
    events.md                    the events it publishes (and their schemas)
  src/
    Brand.Email.Domain/          its own business rules (templates, suppression, rate rules)
    Brand.Email.Application/     use cases, validators, ports (IMailProvider, ISuppressionStore)
    Brand.Email.Infrastructure/  adapters: SES, DynamoDB, SQS
    Brand.Email.Functions/       the Lambda entry points (thin; the host)
    Brand.Email.Client/          the typed client, generated from openapi.yaml
    Brand.Email.Cdk/             the construct: new EmailCapability(scope, id, props)
    Brand.Email.Testing/         the fakes and builders applications use in their own tests
  ui/                            Angular pieces, if any (unsubscribe page, preferences panel)
  tests/
    Brand.Email.Domain.Tests/  Application.Tests/  Infrastructure.Tests/  Functions.IntegrationTests/
    Brand.Email.Cdk.Tests/       assertions on the stack (G-35) and cdk-nag (G-36)
    Brand.Email.Contract.Tests/  the OpenAPI snapshot (G-23) and the port contract suites
  docs/
    how-it-works.md  configuration.md  events.md  threat-model.md  slo.md  runbooks/  changelog.md  primers/
```

It is the same four layers as any application ([Architecture](02-architecture.md)), plus the three parts that make it _consumable_:
`Client`, `Cdk` and `Testing`. Because it follows the standard layout, anyone who has read one capability can navigate every other.

---

## The contract

The contract is the promise between a capability and everything that uses it. It is written down, machine-checked and versioned.

- **HTTP:** an **OpenAPI** document. The service's endpoints are described in it; the generated document is compared with the **approved** file on every
  test run (G-23), so a change to the contract is always a deliberate, reviewed edit.
- **Client:** generated from the same document, so the client cannot drift from the service.
- **Events:** the capability announces what happened (`email.bounced`, `payment.succeeded`) on EventBridge with a documented, versioned schema; other
  parts react without the capability knowing who they are.
- **Errors:** RFC 9457 problem details with stable codes ([document 5](05-validation-and-errors.md)).
- **Configuration:** a typed options class with defaults, validated at deploy and at start.

---

## Using a capability in an app

What the new application does, in full. The names are illustrative; the shape is the promise.

**1. Reference the packages** (restored from the package feed at a pinned version):

```xml
<PackageReference Include="Brand.Email.Cdk" />        <!-- infrastructure, in infra/ -->
<PackageReference Include="Brand.Email.Client" />     <!-- the typed client, in the app -->
<PackageReference Include="Brand.Email.Testing" />    <!-- fakes, in test projects only -->
```

**2. Deploy it as part of the application's stack** (one line, plus the few settings that are the application's own):

```csharp
var email = new EmailCapability(this, "Email", new EmailCapabilityProps
{
    Brand = "Acme", FromAddress = "hello@acme.example", Domain = "acme.example",
});
```

**3. Register the client** (one line in the composition root):

```csharp
services.AddEmailClient(configuration);
```

**4. Call it from a use case through the port** the application owns:

```csharp
await emails.SendAsync(new EmailMessage(to, Template.Welcome, new { name }), cancellationToken);
```

**5. Test it with the fake**, asserting the side effect:

```csharp
Assert.That(fakeEmail.Sent.Single().Template, Is.EqualTo(Template.Welcome), "signing up sends one welcome email");
```

That is the whole job for an application that needs only the defaults. Customization (the brand's templates, a different sender, an extra
event subscriber) is configuration, a template file or a handler the application adds, never a fork of the capability.

---

## How the service code is packaged

The service's Lambda code has to reach the application's AWS account somehow. There are three ways; the choice is a spike in
[phase 2 of the roadmap](11-roadmap.md) and gets its own ADR. The proposal:

| Option                                                                   | Good                                                                                                                                     | Costs                                                                                       |
| ------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| **A. Prebuilt artifact shipped inside the construct package** (proposed) | The application builds nothing; the version of the artifact is the version of the capability; the construct references it as a CDK asset | A build and publish step in the capability's repository; larger packages                    |
| B. The application compiles the capability's functions from NuGet source | Full source visible, one pipeline                                                                                                        | Every application's build is slower and must know how to package the capability's functions |
| C. A container image in a registry                                       | Same artifact everywhere, size limit is large                                                                                            | A registry to run; image scanning to maintain                                               |

Whatever is chosen, **shared code is compiled into each function's package, never a Lambda Layer** (the-hive's rule: layers hide the
dependency and decouple the version from the deploy).

---

## Rules for a capability

1. **One purpose.** It can be described in one sentence without "and".
2. **It owns its data and exposes it only through its contract.** No other code reads its tables.
3. **It is configured, not forked.** If an application needs behaviour the options cannot express, the capability gains a port or an option; the
   application does not copy the code.
4. **It brings its own tests, fakes, alarms, runbooks, threat model and documentation.** A capability without them is not released.
5. **It is idempotent where it can be repeated:** a command carries an idempotency key; an event consumer ignores a repeat.
6. **It fails safely and visibly:** timeouts, retries with backoff and jitter, a dead-letter queue and an alarm on it, a documented degraded mode.
7. **It is multi-tenant correct** where it holds tenant data: the tenant from the token, RLS or a partition key, a two-tenant test.
8. **It has least privilege:** its functions' roles name only what it needs, and the construct asserts it.
9. **It has an SLO,** and its alarms derive from it.
10. **It has no dependency on a specific application.** Brand names, domains, colours and copy arrive through configuration.
11. **It has a deletion story:** what it removes when a person or a tenant is deleted, tested.
12. **It is small enough to understand in a day.** A capability that needs a month to read is two capabilities.

---

## Versioning

**Semantic versioning, with the contract as the public surface.**

| Change                                                                                                        | Version bump | Rule                                                       |
| ------------------------------------------------------------------------------------------------------------- | ------------ | ---------------------------------------------------------- |
| Bug fix, no contract change                                                                                   | patch        | Safe to take                                               |
| New optional field, new endpoint, new event, new option                                                       | minor        | Backward compatible; the OpenAPI diff shows only additions |
| Removed or renamed field, changed meaning, new required input, a deploy that replaces a resource holding data | **major**    | A migration guide is published with it                     |

- **Deprecate before removing:** mark, announce in the changelog, keep for at least one minor release.
- **An automatic OpenAPI diff check** classifies a change as breaking or not and fails the release if the version bump is smaller than the change.
- **Every application pins exact versions** and upgrades deliberately; upgrading is a pull request with the capability's changelog in it.
- **Infrastructure upgrades can be dangerous** (a changed logical id replaces a table). The construct's tests include "does not replace stateful
  resources between versions", and the changelog states which versions need a manual step.
- **A report** lists which version each application uses, so drift is visible (a small script over the applications' lock files).

---

## Talking to a capability

| Need                                                    | Mechanism                                                           | Why                                    |
| ------------------------------------------------------- | ------------------------------------------------------------------- | -------------------------------------- |
| Ask a question or give an order and wait for the answer | HTTP through the typed client (IAM-signed inside the app's account) | Simple, typed, easy to test            |
| Tell the world something happened                       | An event on EventBridge                                             | The sender does not know the receivers |
| Do work that may take time or must survive a failure    | A queue (SQS) with a dead-letter queue                              | Retries and back-pressure for free     |
| Several steps that must all happen                      | A workflow (Step Functions) or a saga of events                     | Visible, retryable, compensating       |

Rules: calls between capabilities are authenticated (IAM, not a shared secret); every call carries the request id and tenant; a capability never
calls the application that called it; and **no capability depends on another** unless the dependency is listed in its README (the dependency graph
is a short list, kept acyclic by a test).

---

## What every capability documents

The [documentation standard](10-documentation-standard.md) applies in full. A capability's `docs/` holds:

| File               | Answers                                                                                                           |
| ------------------ | ----------------------------------------------------------------------------------------------------------------- |
| `how-it-works.md`  | The design in plain words with a picture: the flow of a request, the data, the failure modes                      |
| `configuration.md` | Every option, its default, what it changes, and which are required                                                |
| `events.md`        | Each event it publishes: when, the schema, an example                                                             |
| `threat-model.md`  | The STRIDE review ([document 7](07-security-baseline.md#threat-thinking))                                         |
| `slo.md`           | The objectives and the alarms that watch them                                                                     |
| `runbooks/`        | One file per alarm                                                                                                |
| `changelog.md`     | What changed in each version and what an application must do to upgrade                                           |
| `primers/`         | A primer for each tool the capability introduces ([documentation standard](10-documentation-standard.md#primers)) |

---

## Candidates and where they come from

The first capabilities are the ones **every app already built**, extracted from the app where it is best. A capability is not extracted
until a second application needs it ([strategy](00-strategy.md#what-we-do-not-do)).

| Capability                                       | What it covers                                                                       | Best source today                                                                                | Notes                                                                                         |
| ------------------------------------------------ | ------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------- |
| **Login**                                        | Sign-up, sign-in, passkeys, sessions list, password change, recovery, account delete | Cognito constructs from Hyvva and the-hive; sessions and privacy ideas from `mortgage-analytics` | Use Cognito; do not port the hand-written accounts                                            |
| **Email**                                        | Send, templates, languages, bounce and complaint handling, suppression, dev outbox   | `mortgage-analytics` (templates, layout tests, dev outbox), Hyvva (SES and SQS consumer)         |                                                                                               |
| **Billing**                                      | Stripe checkout, subscriptions, Connect, webhooks, idempotency, reconciliation       | the-hive `services/billing` and `shared/stripe`; Hyvva `Infrastructure/Stripe`                   | Largest overlap across apps                                                                   |
| **Audit trail**                                  | Append-only events, query, retention                                                 | the-hive `shared/admin-audit`; Hyvva `AuditLogger`; buybright `audit.ts`                         | Three versions exist                                                                          |
| **Settings and preferences**                     | Per-account preferences, language, theme, defaults, saved profile                    | `mortgage-analytics` (UserPreferences, Settings page)                                            |                                                                                               |
| **Account data**                                 | Export and delete the person's data                                                  | `mortgage-analytics`                                                                             | Pairs with each capability's deletion story                                                   |
| **Legal acceptance**                             | Terms and privacy versions accepted, with timestamps                                 | the-hive `shared/legal-acceptance`; `mortgage-analytics` Terms page                              |                                                                                               |
| **Feature flags**                                | Rollout and kill switches                                                            | the-hive `shared/feature-flags` (AppConfig); Hyvva (database)                                    | Prefer AppConfig                                                                              |
| **Notifications**                                | In-app, push, SMS fan-out                                                            | Hyvva (push, Telnyx SMS via SQS), Livenhood                                                      | Later                                                                                         |
| **Files**                                        | Upload, scan, store, serve, image optimization                                       | Hyvva (`ImageOptimizer`, S3), Livenhood (documents)                                              | Later                                                                                         |
| **Scheduler**                                    | Delayed and recurring work                                                           | Hyvva (`SchedulerStack`), Livenhood (EventBridge Scheduler)                                      | Later                                                                                         |
| **Mortgage calculation** (a real shared service) | The loan, refinance and strategy maths behind an OpenAPI contract                    | `mortgage-analytics` Domain and Application                                                      | buybright's duplicate math retires into it; the one place a shared running service is natural |

Cross-cutting **libraries** (logging, redaction, request id, clock, rate limiting, security headers, results, test support) are in
`platform-kit`, not capabilities: they have no data and no deployment.

---

## How a capability is born

1. **A second application needs something the first has.** Write down exactly what both need.
2. **Extract from the better implementation,** not by averaging two. Move its tests with it (they are the specification).
3. **Write the contract first** (OpenAPI), then the construct, then the client. The existing app switches to the new capability in the same change,
   proving it by deleting its own copy.
4. **Add what was missing:** fakes, alarms, threat model, runbooks, documentation, primer.
5. **Release 1.0.0** only when two applications run on it.
6. **The first application's tests are the acceptance test:** if they pass against the capability, the extraction is faithful.

---

## Mistakes to avoid

| Mistake                                                       | Why it hurts                                                                     | Instead                                                       |
| ------------------------------------------------------------- | -------------------------------------------------------------------------------- | ------------------------------------------------------------- |
| A capability that serves one app's needs but is named generic | The second app finds it does not fit                                             | Wait for the second app before extracting                     |
| An application reading a capability's database                | Hidden coupling; any change breaks someone                                       | Use the client or an event                                    |
| A configuration option for every difference                   | An unreadable matrix of settings                                                 | Ports and templates for real variation; defaults for the rest |
| A "common" capability that collects leftovers                 | A dumping ground (see [Architecture](02-architecture.md#making-things-findable)) | A capability has one purpose, or it does not exist            |
| Capabilities that call each other in a web                    | A distributed monolith: one failure cascades                                     | Events and an acyclic, short dependency list                  |
| Forking a capability "just for this app"                      | The copy drifts; the fix never arrives                                           | Add a port or option upstream                                 |
| Skipping the fake                                             | Every application mocks it differently and wrongly                               | Ship `Testing` with the capability                            |
