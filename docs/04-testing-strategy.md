# 4. Testing strategy

> Every kind of test we run: the question it answers, the tool, how fast it is, when it runs, and whether it exists today.
> Together they answer one question: "can we change this code tomorrow without being afraid?"

**Read time:** 30 min · **Best for:** deciding which test to write, and knowing what the others already cover ·
**Builds on:** [Quality gates](03-quality-gates.md) · **Reference implementation:** `mortgage-analytics` (about 4,320 tests in
seven suites; its own write-up is `docs/07-testing.md` there)

**On this page:** [The shape](#the-shape) · [The catalog](#the-catalog) · [Which test do I write?](#which-test-do-i-write) ·
[Rules for writing a test](#rules-for-writing-a-test) · [Test doubles](#test-doubles) · [Test data](#test-data) ·
[Detail: the kinds that surprise people](#detail-the-kinds-that-surprise-people) · [Mutation testing](#mutation-testing) ·
[Accessibility: axe and manual](#accessibility-axe-and-manual) · [What each layer proves](#what-each-layer-proves) ·
[What not to test](#what-not-to-test) · [Test speed and the CI budget](#test-speed-and-the-ci-budget) · [Capabilities and their fakes](#capabilities-and-their-fakes)

---

## The shape

Most tests should be fast, small and about one thing; a few should be slow and cover a whole journey. Our shape is a
**diamond**, not a thin pyramid: a wide base of unit tests, a thick middle of integration tests (because most of the risk in
a serverless application is in the seams between the code and the cloud), and a narrow top of end-to-end journeys.

```
                      /\
                     /  \          few: end-to-end journeys, accessibility, cross-browser, load
                    /----\
                   /      \        many: integration, contract, tenant isolation, infrastructure
                  /--------\
                 /          \      most: unit tests of rules, mappers, validators, pure functions
                /____________\
        around all of it, checking the tests themselves:  mutation, conventions, architecture, coverage
```

Two ideas hold the diamond together:

- **A test is only as good as its ability to fail.** Coverage says code ran; mutation testing says a change to the code would have
  been noticed ([Mutation testing](#mutation-testing)).
- **Tests that read our code** (conventions, architecture) protect the standards the same way ordinary tests protect behaviour.

---

## The catalog

Status: **Exists** in `mortgage-analytics` today; **Elsewhere** exists in Hyvva or the-hive and is generalized into the standard;
**New** is added by this platform.

### Verify that the code does what it should

| #   | Kind                             | The question it answers                                               | Tool                                                 | Speed      | Tier | Status                                                                  |
| --- | -------------------------------- | --------------------------------------------------------------------- | ---------------------------------------------------- | ---------- | ---- | ----------------------------------------------------------------------- |
| 1   | **Domain unit**                  | Is each business rule right, at its boundaries?                       | NUnit                                                | under 1 s  | 1, 2 | Exists                                                                  |
| 2   | **Application unit**             | Does a use case map, validate and decide correctly?                   | NUnit, NSubstitute, `FakeTimeProvider`               | under 1 s  | 1, 2 | Exists                                                                  |
| 3   | **Validator tests**              | Does every input rule accept and reject at its edges?                 | NUnit, FluentValidation test helpers                 | under 1 s  | 1, 2 | Exists (Hyvva ratchets that every validator has a test)                 |
| 4   | **Frontend unit and component**  | Does a component, store or pure function behave?                      | Jest, jest-preset-angular, Angular TestBed           | about 10 s | 1, 2 | Exists                                                                  |
| 5   | **Integration (API)**            | Does a real request through the real pipeline return the right thing? | `WebApplicationFactory`, NUnit                       | about 2 s  | 2    | Exists                                                                  |
| 6   | **Integration (data)**           | Does the repository work on a real database, migrations included?     | EF Core, SQLite in memory; Testcontainers PostgreSQL | 1 to 5 s   | 2, 4 | Exists                                                                  |
| 7   | **Stored-format compatibility**  | Can today's code load what yesterday's code saved?                    | NUnit with saved JSON files                          | under 1 s  | 2    | Exists                                                                  |
| 8   | **Email and document rendering** | Does the email or PDF contain what it should, in every language?      | NUnit with HTML assertions                           | under 1 s  | 2    | Exists                                                                  |
| 9   | **Time-based behaviour**         | Does expiry, reminder or schedule work after a month passes?          | `FakeTimeProvider`, `host.Clock.Advance`             | under 1 s  | 2    | Exists                                                                  |
| 10  | **Concurrency and race**         | Do two requests at once corrupt anything or compute twice?            | NUnit with controlled tasks                          | under 1 s  | 2    | Exists                                                                  |
| 11  | **Failure injection**            | What happens when mail, database or a partner is down or slow?        | NSubstitute throwing, fake partner                   | under 1 s  | 2    | Exists (`AccountEmailFailureTests`); to extend to timeouts and retries  |
| 12  | **Idempotency replay**           | Is the second delivery of the same webhook or message harmless?       | NUnit with a fake store                              | under 1 s  | 2    | Elsewhere (the-hive has the idempotency store; replay tests to confirm) |
| 13  | **Property-based**               | Does a rule hold for thousands of generated inputs?                   | FsCheck (C#), fast-check (TypeScript)                | 1 to 5 s   | 2    | **New** (candidate for money, parsing, schedules)                       |

### Verify the seams and the contracts

| #   | Kind                          | The question it answers                                                           | Tool                                                  | Speed      | Tier | Status                                  |
| --- | ----------------------------- | --------------------------------------------------------------------------------- | ----------------------------------------------------- | ---------- | ---- | --------------------------------------- |
| 14  | **API contract snapshot**     | Did the public JSON change, and did someone approve it?                           | Approved OpenAPI file compared on test                | under 1 s  | 2    | Exists                                  |
| 15  | **Port contract suite**       | Does every implementation of a port (fake and real) behave the same?              | One abstract NUnit suite per port                     | 1 to 5 s   | 2    | **New** as a standard                   |
| 16  | **Generated client check**    | Does the typed client compile against the contract it came from?                  | Build step                                            | seconds    | 2    | **New**                                 |
| 17  | **Tenant isolation**          | Can tenant A ever see or change tenant B's data?                                  | Integration tests with two tenants; RLS tests         | 2 to 5 s   | 2    | Elsewhere (Hyvva)                       |
| 18  | **Authorization matrix**      | Is every endpoint protected, for every role?                                      | A test that enumerates endpoints and asserts a policy | 1 s        | 2    | Elsewhere (Hyvva: admin and tier gates) |
| 19  | **Infrastructure assertions** | Does each stack create exactly what it must (encryption, retention, permissions)? | CDK assertions (NUnit)                                | about 10 s | 2    | Elsewhere (Hyvva, the-hive)             |
| 20  | **Infrastructure policy**     | Any wildcard permission, public bucket, missing MFA?                              | cdk-nag, IAM policy lint                              | seconds    | 2    | Elsewhere (the-hive)                    |

### Verify the code itself and the standards

| #   | Kind                         | The question it answers                                             | Tool                                     | Speed          | Tier | Status                                             |
| --- | ---------------------------- | ------------------------------------------------------------------- | ---------------------------------------- | -------------- | ---- | -------------------------------------------------- |
| 21  | **Conventions**              | Do the files follow our written style?                              | Roslyn (C#), ESLint (TypeScript), regex  | under 1 s      | 2    | Exists (15 tests)                                  |
| 22  | **Architecture**             | Do the layers only depend inward? Is anything banned present?       | NetArchTest, scans                       | about 1 s      | 2    | Elsewhere (Hyvva); `ContractLayeringTests` exists  |
| 23  | **Ratchets**                 | Has the number of violations of a rule risen?                       | Scan tests with a stored ceiling         | about 1 s      | 2    | Elsewhere (Hyvva)                                  |
| 24  | **Mutation**                 | Would the tests notice if the code were subtly wrong?               | Stryker.NET; Stryker with Jest           | minutes        | 3, 4 | Exists (domain in CI; others local)                |
| 25  | **Coverage**                 | Which code did the tests never run?                                 | coverlet; Jest coverage                  | with the tests | 2    | Exists                                             |
| 26  | **Independent oracle**       | Does a second, independent implementation of the maths agree?       | Python models compared with the live API | about 2 min    | 4    | Exists                                             |
| 27  | **Documentation**            | Do the docs match the code: counts, links, file and line citations? | Python check script                      | seconds        | 2    | Exists (counts); links and citations to generalize |
| 28  | **Translation completeness** | Does every language have every message?                             | Jest test over the dictionaries          | under 1 s      | 2    | Exists                                             |

### Verify what a person experiences

| #   | Kind                            | The question it answers                                                                                    | Tool                                                            | Speed      | Tier            | Status                                                                    |
| --- | ------------------------------- | ---------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------- | ---------- | --------------- | ------------------------------------------------------------------------- |
| 29  | **End-to-end journeys**         | Can a person actually do the main things, in a real browser?                                               | Playwright (Chromium)                                           | about 30 s | 2               | Exists (508 tests at 1 Nov)                                               |
| 30  | **Accessibility (automated)**   | Any detectable WCAG violation on any page, theme or language?                                              | axe-core with Playwright                                        | within e2e | 2               | Exists                                                                    |
| 31  | **Accessibility (keyboard)**    | Can everything be reached and used with a keyboard, in sensible order?                                     | Playwright tab-walk, focus assertions                           | within e2e | 2               | Exists                                                                    |
| 32  | **Reader-loss**                 | Is anything lost at 2x text, WCAG text spacing, 320 px, forced colours, reduced motion, touch-target size? | Playwright with browser flags                                   | within e2e | 2               | Exists (reduced-motion styles exist; an e2e check for them is to confirm) |
| 33  | **Layout stability**            | Does the page jump while loading?                                                                          | Layout-shift observer in Playwright                             | within e2e | 2               | Exists                                                                    |
| 34  | **Responsive and print**        | Does the layout hold at phone, tablet, computer; does print work?                                          | Playwright viewports, PDF checks                                | within e2e | 2               | Exists                                                                    |
| 35  | **Cross-browser**               | Does it work in Firefox and Safari's engine?                                                               | Playwright Firefox, WebKit                                      | minutes    | 3, 4            | Exists (manual script)                                                    |
| 36  | **Visual regression**           | Did a component's pixels change unexpectedly?                                                              | Playwright `toHaveScreenshot` on the UI kit's component gallery | seconds    | 2 (UI kit only) | **New**                                                                   |
| 37  | **Manual assistive technology** | Does a real screen reader read it sensibly? Does it work on a real phone?                                  | NVDA, VoiceOver, a phone, with a written script                 | an hour    | 4               | Open items in the app's backlog                                           |

### Verify the system under stress and in production

| #   | Kind                               | The question it answers                                      | Tool                                             | Speed          | Tier      | Status                                       |
| --- | ---------------------------------- | ------------------------------------------------------------ | ------------------------------------------------ | -------------- | --------- | -------------------------------------------- |
| 38  | **Size budget**                    | Is the initial download within budget?                       | Angular build budget                             | with the build | 2         | Exists                                       |
| 39  | **Performance budget**             | Are LCP, CLS and blocking time within budget?                | Lighthouse CI                                    | minutes        | 3         | Elsewhere (Hyvva)                            |
| 40  | **Load and capacity**              | How many requests a second, and what happens past the limit? | A script that replays the page's requests        | minutes        | 4         | Exists                                       |
| 41  | **Security scan: dependencies**    | Any known vulnerable package?                                | `dotnet list package --vulnerable`, `npm audit`  | seconds        | 3         | Elsewhere (Hyvva, the-hive)                  |
| 42  | **Security scan: secrets**         | Any secret in the code or history?                           | gitleaks                                         | seconds        | 1, 3      | **New**                                      |
| 43  | **Security scan: static analysis** | Any known insecure pattern in our code?                      | Roslyn security analyzers, ESLint security rules | in the build   | 2         | **New** (analyzers: no CI cost)              |
| 44  | **Security scan: running app**     | Does the running app have common web weaknesses?             | OWASP ZAP baseline, run locally                  | minutes        | 4         | **New**                                      |
| 45  | **Threat-model review**            | What could an attacker do, and is each path closed?          | STRIDE write-up per capability                   | a meeting      | design    | Elsewhere (the-hive)                         |
| 46  | **Post-deploy smoke**              | Does the real, deployed system answer?                       | A canary on a schedule                           | seconds        | deploy    | Elsewhere (the-hive)                         |
| 47  | **Restore drill**                  | Can the data really be restored from a backup?               | A scripted restore into a scratch environment    | an hour        | quarterly | Open in `mortgage-analytics` going-live plan |

---

## Which test do I write?

Start from the question, not the tool.

| If the change is...                  | Write first                                                           | Then                                                                  |
| ------------------------------------ | --------------------------------------------------------------------- | --------------------------------------------------------------------- |
| A business rule                      | A Domain unit test that pins the numbers (from an independent source) | A mutation run on the rule                                            |
| A new field on a request             | A validator test at its edges                                         | A contract snapshot update (deliberate), a client regeneration        |
| A new use case                       | An Application unit test with the ports substituted                   | An integration test through the endpoint                              |
| A new database column or table       | A repository test on a real database, a migration test                | A stored-format test if it is saved JSON                              |
| An integration with AWS or a partner | A port, a fake, and a port contract suite                             | One test that proves the _side effect_ (the call, with its arguments) |
| An endpoint                          | An authorization test (unauthenticated, wrong role, wrong tenant)     | An integration test of the happy path and each error                  |
| A screen                             | A component test; an e2e journey; an axe check                        | Reader-loss, responsive, layout-shift, translation completeness       |
| A fix for a bug                      | A test that reproduces the bug (red)                                  | The fix (green); the test stays forever                               |
| A refactoring                        | None: the existing tests are the safety net                           | If a test must change, the refactoring changed behaviour              |

---

## Rules for writing a test

1. **Arrange, act, assert.** Three visible blocks, in that order, one act per test.
2. **One behaviour per test**, and a name that reads as a sentence: `Subject_Condition_Outcome`
   (`AddingAPasskey_WhileTheMailServiceIsDown_StillAddsIt`). A failure message should tell you what broke without opening the file.
3. **Every assertion has a message** saying _what should be true and why_. A conventions test fails otherwise (it exists).
   Several assertions in one test go in `Assert.Multiple`, so one failure does not hide the next (also enforced).
4. **No logic in a test.** No `if`, no loop deciding what to assert. A test that needs logic needs to be two tests, or a
   parameterized test (`[TestCase]`, `[TestCaseSource]`).
5. **Deterministic:** no real clock, no real network, no random without a fixed seed, no `Thread.Sleep`. Time comes from
   `FakeTimeProvider`; waiting is done by waiting for a condition (Playwright's auto-wait), never for a duration.
6. **Isolated:** a test builds its own world (its own host, its own database) and leaves nothing behind. Tests run in any order and in parallel.
7. **Test behaviour, not implementation.** Assert what a caller can observe. A test that breaks when you rename a private method
   was testing the wrong thing.
8. **Independent expected values.** Expected numbers come from outside the code under test: a hand calculation, a regulator's
   example, a second implementation (the oracle). A test that recomputes the answer with the same formula proves nothing.
9. **Tests are production code:** the same naming, braces, formatting and review as anything else.
10. **Never delete a failing test to get green.** Fix the code or, if the test is wrong, fix the test and say why.

---

## Test doubles

A **test double** stands in for a real collaborator. The words are often confused, so they are fixed here (after Gerard Meszaros).

| Kind      | What it does                                                  | Example                                                  |
| --------- | ------------------------------------------------------------- | -------------------------------------------------------- |
| **Dummy** | Fills a parameter and is never used                           | A logger nobody reads                                    |
| **Stub**  | Returns canned answers                                        | A rates provider that always returns 6.5%                |
| **Fake**  | A real, simpler implementation                                | An in-memory repository; SQLite in memory for a database |
| **Spy**   | Records how it was called, for the test to ask afterwards     | A mail transport that stores every message sent          |
| **Mock**  | Is told what calls to expect, and fails if they do not happen | "`SendAsync` must be called once with this message"      |

Our rules:

- **Prefer the real thing, then a fake, then a stub; use a mock only for a side effect the test must prove.** Over-mocking makes tests
  that pass while the system is broken.
- **Double only ports we own** (the interfaces in the Application layer). Never double a framework or SDK type directly: wrap it
  in a port first, then double the port.
- **A double is declared in the test or in a shared fake**, never copy-pasted per test.
- NSubstitute is the tool for stubs, spies and mocks. How it works is in its [primer](primers/nsubstitute.md).

---

## Test data

- **Builders** make test objects with sensible defaults and a fluent way to change only what the test is about
  (`A.Loan().WithRate(6.5m).Build()`), so a test shows the one fact it cares about. (`mortgage-analytics` has `Testing/A.cs` and
  `Builders/`; the-hive's standards call the pattern "Object Mother".)
- **Reference data** is a small set of named, documented cases whose answers were verified independently (`ReferenceLoans`,
  `ReferenceRefinances`), shared across test projects from one `Testing` project.
- **No production data, ever.** Test data is invented; personal data patterns are obviously fake (`dev@localhost`).
- **A shared test base class** is allowed for repeated setup (`UseCaseTestBase<T>` in the-hive) but must not hide the act and assert.

---

## Detail: the kinds that surprise people

**Conventions tests (21).** A normal test runs the code; these tests read the _source files_ with the Roslyn compiler library, build
the syntax tree, and assert facts about it ("no `var` where the type is not visible", "every `Assert.That` has a message"). They
turn a style guide into a failing build. See [Roslyn and conventions tests](primers/roslyn-conventions-tests.md).

**Architecture tests and ratchets (22, 23).** They check the shape of the system: which assemblies reference which, which names
are banned. A **ratchet** records how many violations exist now and fails if the number rises, so a rule can be adopted on old
code without a flag day. See [NetArchTest and ratchets](primers/netarchtest-and-ratchets.md).

**Port contract suites (15).** For every port, one abstract suite of tests (`SavedCalculationRepositoryContract`) is inherited by
a class for each implementation (the in-memory fake, the real database). They prove the fake is faithful, which is what makes
tests that use the fake trustworthy.

**Independent oracle (26).** A second implementation, written separately in a different language, run against hundreds of
random households and compared field by field. When the two agree to the cent the chance that both are wrong the same way is
small. It is also how documentation numbers stay true: the docs' charts are computed from the reference loans and the build stops if a number disagrees.

**Failure injection (11) and idempotency (12).** In the cloud a call can fail, time out or arrive twice. The tests make those happen
on purpose: the mail transport throws, the webhook is delivered twice. A feature is not done until its failure behaviour is a
decision with a test, not an accident.

**Visual regression (36).** Screenshot comparison catches unintended pixel changes but is brittle. We use it only on the UI kit's
component gallery (stable fixtures, one browser, one font set), not on whole application pages.

**Property-based (13).** Instead of picking examples, the test states a _property_ ("a schedule ends at exactly zero for any valid
loan") and a library generates many inputs. Good for money, parsers and invariants. Used sparingly; the example tests remain the
specification.

---

## Mutation testing

**What it is.** A tool changes your code in small ways (a `<` becomes `<=`, a `+` becomes `-`, a line is deleted), one change at a
time (a **mutant**), and runs the tests for each. If a test fails, the mutant is **killed** (good: the tests noticed). If every test
still passes, the mutant **survived** (bad: the tests cannot tell the wrong code from the right code). The **mutation score** is
killed divided by total.

**Why it matters.** Coverage only says a line _ran_. A test with no assertions gives 100% coverage and 0% protection. Mutation
testing measures protection. It adds no tests by itself; each survivor is a diff showing a behaviour nobody asserted, and the fix is
the test that would have failed.

**How we run it.** Stryker.NET for C# and Stryker with the Jest runner for TypeScript logic. Thresholds are in
[G-22](03-quality-gates.md#the-gates): the Domain breaks the build under 95%. It is slow, so it runs in the nightly and release
tiers (and the domain run stays in CI because it takes about two minutes). Full explanation: [Stryker primer](primers/stryker.md).

**A survivor is a finding, not a failure to explain away.** Triage each one: write the missing test, or mark the mutant as
equivalent (the code change cannot change behaviour) with a written reason.

---

## Accessibility: axe and manual

Automated tools find roughly a third of accessibility problems; the rest need a human. We use both on purpose.

- **axe-core** (via Playwright) runs on every page, in light and dark themes and in each language, and must find zero violations.
- **Keyboard tests** walk the page with Tab and assert focus order, visible focus, no trap, and that Escape closes what opened.
- **Reader-loss tests** zoom the text to 200% and 32 px, apply the WCAG text-spacing rules, shrink to 320 px, force high-contrast
  colours, and assert nothing is clipped, overlapped or lost.
- **Manual passes** are scheduled and scripted: a screen reader (NVDA on Windows, VoiceOver on a phone), a real phone, a keyboard-only
  session. They are tier 4 and each leaves a dated note.

Primers: [axe-core](primers/axe-core.md) and [Playwright](primers/playwright.md).

---

## What each layer proves

| Layer / project                  | Proves                                                                                                                    | Never needs                   |
| -------------------------------- | ------------------------------------------------------------------------------------------------------------------------- | ----------------------------- |
| **Domain.Tests**                 | The rules and invariants: formulas against known values, boundaries, every guard by its message                           | Any mock, database or network |
| **Application.Tests**            | Mapping decisions, validation, use-case orchestration, with ports substituted; sociable tests through the real domain     | A database, HTTP              |
| **Infrastructure.Tests**         | Repositories on a real database built by the real migrations; migrations match the model; stored formats load             | HTTP                          |
| **Api.IntegrationTests**         | Real requests through the real pipeline: status codes, headers, authorization, errors, logging, rate limits, the contract | A browser                     |
| **Conventions and Architecture** | The standards and the layer rules                                                                                         | The application running       |
| **Frontend unit (Jest)**         | Components, stores, pipes and pure functions                                                                              | A real server or browser      |
| **e2e (Playwright)**             | Journeys, accessibility, reader-loss, layout, responsive, language, security headers, print                               | Backend detail                |
| **Infra tests (CDK)**            | Stack contents and policy                                                                                                 | AWS                           |
| **Tools (oracle, load, docs)**   | Independent correctness, capacity, documentation accuracy                                                                 | The CI                        |

---

## What not to test

Tests cost time forever. Do not test:

- **The framework or the library** ("does EF Core save a row?"). Test _our use_ of it.
- **A trivial property or a pass-through** with no logic.
- **Private methods** directly. Reach them through public behaviour; if that is hard, the class has the wrong shape.
- **The same thing at every level.** A rule is pinned once, at the lowest level that can express it; higher levels test the
  _wiring_, not the rule again.
- **Pixel positions in an application page** (use the layout checks and the UI kit's gallery).

---

## Test speed and the CI budget

| Suite                                 | Target                         | Why                                             |
| ------------------------------------- | ------------------------------ | ----------------------------------------------- |
| Domain and Application unit           | under 5 s in total             | Run on every save; a slow suite stops being run |
| All backend tests                     | under 1 minute                 | Tier 1 and 2                                    |
| Jest                                  | under 30 s                     | Tier 1 and 2                                    |
| Playwright (Chromium)                 | under 5 minutes with 4 workers | Tier 2; split by spec group if it grows         |
| Mutation, cross-browser, load, oracle | any length                     | Tier 3 or 4; never on every push                |

A suite that creeps past its target is a defect: profile it, parallelize it, or move the slow part to a higher tier.

---

## Capabilities and their fakes

Every [capability module](08-capability-modules.md) ships with a **testing package** so the applications that use it never need AWS
to run their tests:

- an **in-memory fake** of its client (`Brand.Email.Testing`: a spy that records every message "sent"),
- the **port contract suite**, so the fake is proved faithful to the real adapter,
- **builders** for its request types,
- a **sample integration test** showing how an app asserts the side effect.

An application's tests assert that the email was sent with the right arguments by reading the fake, and the capability's own repository
proves the real adapter sends it ([definition of done, item 6](03-quality-gates.md#definition-of-done)).
