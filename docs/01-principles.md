# 1. Principles

> The ideas behind every rule in these documents: KISS, DRY, YAGNI, SOLID, Clean Code, TDD and a few more. Each one is
> explained in plain words, shown in our own code, and tied to what enforces it.

**Read time:** 25 min · **Best for:** anyone new, before writing code · **Next:** [Architecture](02-architecture.md)

**On this page:** [How to use this page](#how-to-use-this-page) · [KISS](#kiss) · [DRY](#dry) · [YAGNI](#yagni) ·
[SOLID](#solid) · [Clean Code](#clean-code) · [TDD](#tdd) · [Fail fast and parse at the edge](#fail-fast-and-parse-at-the-edge) ·
[Immutability and pure functions](#immutability-and-pure-functions) · [Explicit over implicit](#explicit-over-implicit) ·
[Composition over inheritance](#composition-over-inheritance) · [Deterministic time](#deterministic-time) ·
[Secure and observable by default](#secure-and-observable-by-default) · [When principles conflict](#when-principles-conflict) ·
[Summary: what enforces what](#summary-what-enforces-what)

---

## How to use this page

Each principle has the same five parts: **the idea in one line**, **in plain words**, **an example from our code**, **how it goes
wrong**, and **what enforces it**. Examples come from `mortgage-analytics` unless stated, because it is the reference
implementation (see its own `docs/09-solid-by-example.md` for a longer SOLID walk-through).

Principles are not laws. They are the reasons behind the rules. When a rule seems to get in the way, come back here and
ask which principle it serves.

---

## KISS

**Keep it simple.** The simplest thing that is correct and clear is the right thing.

**In plain words.** Every line of code is something a stranger must read, a test must cover and a bug can hide in. Complexity is
a cost paid forever; cleverness is borrowed against the next reader. If two designs work, take the one a new teammate
understands without help.

**Ours.** The monthly payment is one formula in one place (`PaymentFormula`), written the way the textbook writes it. The API
has one endpoint per question the user asks, not a generic "query" endpoint with options.

**Going wrong.** A framework invented for a problem we have once. A generic `Handler<TRequest, TResponse, TContext>` for three
handlers. A configuration file that re-implements the programming language.

**Enforced by.** Review. Partly by gates that force small units: maximum nesting depth of two, guard clauses over nested
conditionals (Hive's engineering standards), and an analyzer limit on method length and complexity (to be added to the
standards kit, [document 3](03-quality-gates.md)).

---

## DRY

**Don't repeat yourself.** Every piece of _knowledge_ has one home.

**In plain words.** The thing to avoid duplicating is not text, it is a _decision_. Two functions that look alike but change
for different reasons are not duplicates; merging them couples things that should be free. Two places that state the same
business rule _are_ duplicates, and one will be updated while the other is forgotten.

**Ours, a good merge.** The adjustable-rate and buydown mapping is one `RateOptionMapper`, used by the purchase, refinance and
strategy mappers (`AmortizationMapper`, `RefinanceMapper`, `StrategyMapper`), so a rule about rate caps is written once.

**Ours, a known miss.** The 30% rate limit is still written in two places in `mortgage-analytics` (the owner's notes list it as a
weak spot). That is exactly the kind of duplicated knowledge this principle is about, and the platform's validation kit
([document 5](05-validation-and-errors.md)) states each limit once and shares it.

**Ours, the platform exists because of DRY.** Hyvva and the-hive each wrote a 39-line PII redaction pattern list; the-hive's
billing and booking-site services each wrote the same 64-line webhook idempotency repository. That is the knowledge "what counts as
personal data in a log" and "have we seen this webhook before" stated more than once.

**Going wrong, the opposite way.** A shared base class used by three things that only look similar, then bent with flags until
nobody dares touch it. Duplication is cheaper than the wrong abstraction. The working rule: **wait for the third copy before
abstracting** (inside one app) and **wait for the second app before extracting to the platform**
([strategy](00-strategy.md#what-we-do-not-do)).

**Enforced by.** Review, plus the shared test helpers (`UseCaseTestBase`, fluent builders in the-hive's standards) that remove
duplicated test setup. A duplicate-code report is a candidate gate; it is listed in [document 3](03-quality-gates.md) as
advisory, because a naive threshold produces noise.

---

## YAGNI

**You aren't gonna need it.** Build what is needed now.

**In plain words.** A feature built "in case" costs time now, costs tests and documentation, and is usually wrong when the need
arrives. Leaving a door open is free; building the room behind it is not.

**Ours.** The roadmap in `mortgage-analytics` records what was deliberately **not** built and why: a biweekly view (the form already
converts it), a tax-year selector (2027 figures not published). The-hive's brief says: do not build Commerce or AI features, even
if a task looks related.

**Going wrong.** A capability designed for imagined users, with options nobody asked for.

**Enforced by.** Review, and the [Roadmap](11-roadmap.md) rule that a capability needs a working app behind it first.

---

## SOLID

Five design rules for classes and modules, from Robert C. Martin. Together they make code that is easy to change in one place
without breaking another.

### S: single responsibility

**A unit has one reason to change.**

In plain words: if you can describe a class with "and", it is two classes. "Validates the request _and_ saves it _and_ sends the
email" is three jobs, and a change to the email text risks the saving.

Ours: `ExtraPaymentGuard` holds only the shared guard clauses for the rule constructors (negative amount, payment number below 1);
`ExtraPaymentPlan` only holds the payments; `AmortizationCalculator` only runs the schedule. Each has its own tests.

Enforced by: one type per file (a conventions test), small files, and the layer rules in [Architecture](02-architecture.md).

### O: open for extension, closed for modification

**Add behaviour by adding code, not by editing working code.**

In plain words: when a new kind of thing arrives, you should write a new class, not add another `if` to a method that already works.

Ours: extra payments are an interface, `IExtraPaymentRule`, with one class per kind (`MonthlyExtraPayment`, `AnnualExtraPayment`,
`LumpSumExtraPayment`). A fourth kind is a fourth class; the calculator does not change. (This is the example talked through in
the interview deck as "what if the business asked".)

Going wrong: a growing `switch` on a type code in five places.

Enforced by: review; the mutation test run shows when a branch is never exercised, which is how a hidden `switch` is found.

### L: Liskov substitution

**Anything that implements an interface must be usable wherever that interface is expected, without surprises.**

In plain words: if the calculator accepts an `IExtraPaymentRule`, every rule must behave as one: no rule may throw "not supported"
for a normal call or return nonsense because it is a special case.

Ours: the interface is one method, `AmountFor(paymentNumber)`, so there is little room for a rule to break it, and
`ExtraPaymentRuleTests` uses a parameterized `[TestCaseSource]` over the rules' invalid constructions. The platform goes one step
further and makes it a standard: **one contract test suite per port, run against every implementation** (the in-memory fake
and the real adapter run the same tests), so a substitute that misbehaves fails at once. That standard is new; it is not yet
in `mortgage-analytics`.

Enforced by: contract test suites per port (to add to the standard) and review.

### I: interface segregation

**Many small interfaces beat one large one.** A caller should depend only on what it uses.

Ours: the mail layer exposes `IMailTransport` with one method, `SendAsync(message, cancellationToken)`. A test that needs to simulate
a mail outage substitutes that single method (`AccountEmailFailureTests`), not a 30-method "email service".

Going wrong: an `IRepository` with every possible query, which every fake must then implement.

Enforced by: review; NSubstitute analyzers flag substituting what cannot be substituted; the primer shows how a small interface
keeps tests short ([NSubstitute](primers/nsubstitute.md)).

### D: dependency inversion

**High-level code depends on abstractions, and low-level code implements them.**

In plain words: the use case says "I need something that can save a calculation" (an interface it owns). The database code
provides it. The arrow of _source-code dependency_ points inward even though the arrow of _runtime calls_ points outward.

Ours: `ISavedCalculationRepository` is declared in Application; `SavedCalculationRepository` is in Infrastructure. SQLite was
replaced by PostgreSQL as the option without changing a use case.

Enforced by: project references (the compiler) and the architecture tests in [Architecture](02-architecture.md#how-each-rule-is-enforced).

---

## Clean Code

A set of habits (from Robert C. Martin's book and the community around it) that make code read like prose.

| Habit                                                                 | In plain words                                                                                            | Enforced by                                               |
| --------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- | --------------------------------------------------------- |
| **Names reveal intent**                                               | `LumpSumExtraPayment` says what it is; `Data2` does not. Names are at least three letters.                | Roslyn naming conventions test; ESLint `id-length`        |
| **Small functions, one level of abstraction**                         | A function either orchestrates steps or does one step; it does not mix the two                            | Review; analyzer limits on length and complexity (to add) |
| **Guard clauses, shallow nesting**                                    | Return or throw early for the bad cases so the main path is not indented. Maximum nesting depth two.      | Analyzer rule (to add); review                            |
| **No comments that say _what_**                                       | If a comment explains what a block does, extract a method with that name. Comments explain _why_, rarely. | Review                                                    |
| **Public API is documented**                                          | Every public member has an XML summary; the same text feeds IntelliSense and OpenAPI                      | Compiler: missing summary (CS1591) fails the build        |
| **No magic numbers or strings**                                       | A named constant or a configuration option says what `0.55` is                                            | Review; analyzer for repeated literals (candidate)        |
| **No boolean flag parameters**                                        | `Save(true)` is unreadable; two methods or an enum says it                                                | Review                                                    |
| **Command-query separation**                                          | A method either changes state or returns a value, not both                                                | Review                                                    |
| **No hidden side effects**                                            | A method named `Get...` does not write; a constructor does not call the network                           | Review                                                    |
| **One type per file; braces always; `var` only where the type shows** | The shape of the code is uniform, so differences carry meaning                                            | Conventions tests; `.editorconfig` in the build           |
| **Format everything the same way**                                    | Formatting is a machine's job, never a review topic                                                       | Prettier, `dotnet format`, `ruff`; the build fails        |
| **Delete dead code**                                                  | Version control remembers it                                                                              | Analyzer: unused usings, members (IDE0005, IDE0051)       |

---

## TDD

**Test-driven development: write a failing test first, then the smallest code that passes, then clean up.** The cycle is
**red, green, refactor**.

```
   +--> RED     write one test for behaviour that does not exist; run it; watch it fail for the right reason
   |      |
   |      v
   |    GREEN   write the smallest change that makes it pass; run all tests
   |      |
   |      v
   +--- REFACTOR  improve the design with the tests as a safety net; run all tests again
```

In plain words: the test is the specification, written before the thing it specifies. Watching it fail first proves the test
can fail; a test that has never been red might not test anything. It also keeps the design honest, because code that is hard
to test is usually code with too many dependencies.

Ours: every feature in `mortgage-analytics` starts with the independent model (a Python reference calculation), then a failing
domain test pinning the reference values, then the smallest domain change, then outward layer by layer (its `docs/07-testing.md`
tells the ARM margin story step by step). The conventions tests exist because a convention nobody checks is forgotten.

Going wrong: tests written afterwards to reach a coverage number. They confirm what the code does, not what it should do. This
is why mutation testing is a gate ([Testing strategy](04-testing-strategy.md#mutation-testing)).

Enforced by: coverage and mutation thresholds (indirectly), the definition of done, and review. **A change without a test that
failed first is not done.** The commit history should show it where practical (a test commit before or with the change).

---

## Fail fast and parse at the edge

**Reject bad input at the boundary, once, and from then on work with values that are known to be valid.**

In plain words: the edge of the system (an HTTP request, a form, a queue message, a file) is where untrusted data enters. Check
it there, turn it into a typed object (`Money`, `EmailAddress`, a validated request), and let the inside trust the types. Do not
sprinkle `if (x == null)` through business code.

Two linked ideas:

- **Parse, don't validate.** A check that returns `true` leaves you holding the same unchecked value; a parse returns a _new type_
  that can only exist if the value was valid. After the parse, the compiler remembers the check for you.
- **Make illegal states unrepresentable.** If a loan with a term of minus five years must never exist, make the constructor refuse to
  build one. The bug cannot be written, so it cannot be tested for.

Ours: `AmortizationRequestValidator` (FluentValidation) checks every field at the API boundary before any use case runs; domain
constructors throw a `DomainValidationException` for values that should never get that far; a "guard limit" test pins each message.
Full detail in [Validation and errors](05-validation-and-errors.md).

Enforced by: a validator test per request type (Hyvva's `ValidatorTestPresenceRatchetTests` fails when a validator has no test),
the architecture rule that contracts are separate from domain types, and tests at each boundary value.

---

## Immutability and pure functions

**Prefer values that never change after they are made, and functions whose result depends only on their inputs.**

In plain words: a pure function can be understood, tested and cached without knowing anything else about the program. Shared
mutable state is the source of most "works on my machine" bugs and nearly all concurrency bugs, and on Lambda a single instance
handles requests one after another, so state left behind by one request leaks into the next.

Ours: the whole domain is pure; the Angular formatting helpers (`monthsInWords`) are pure functions in their own files; records and
read-only collections are the default.

Enforced by: C# `readonly` and records by convention; the analyzers (`IDE0044` make fields readonly); review.

---

## Explicit over implicit

**Say what you mean in the code, and avoid magic that works at a distance.**

In plain words: configuration read from a place nobody can find, a behaviour attached by a base class three levels up, a
constructor that registers itself in a global: each is convenient once and baffling after. Make dependencies arguments, make
registration visible in one composition root, make options typed classes validated at startup.

Ours: `Program.cs` is the one place that wires everything; every setting is a typed options class; the packages are pinned by
lock files so a restore cannot change versions silently.

Enforced by: locked-mode restore in CI, options validation on start, architecture rules on where wiring lives.

---

## Composition over inheritance

**Build behaviour by combining small objects, not by deriving from a large one.**

In plain words: inheritance couples a child to everything its parent ever does. Composition ("has a") couples it only to the
interface it uses. Test base classes are a deliberate exception (the-hive's `UseCaseTestBase<T>` removes duplicated mock setup);
production code rarely needs inheritance beyond interfaces.

Enforced by: review; a sealed-by-default convention (`internal sealed class`) already used in `mortgage-analytics`.

---

## Deterministic time

**Code never reads the clock directly; time is an input.**

In plain words: `DateTime.Now` makes a test depend on _when_ it runs and _where_ (local time zones). Inject a `TimeProvider`, and a
test can freeze it or advance it by a month. Expiry, reminders, schedules and "what month is the first payment" all depend on this.

Ours: the whole application takes `TimeProvider`; tests use `FakeTimeProvider`. A real incident: a frozen test clock against a
real cookie jar expired sign-in cookies and failed 59 tests only in CI.

Enforced by: a ratchet test that bans `DateTime.Now` in production code (Hyvva `DateTimeNowBanRatchetTests`, to be generalized).

---

## Secure and observable by default

Two principles that cost little when built in and a great deal when added later.

- **Secure by default, least privilege.** Every endpoint requires authorization unless it is explicitly marked public; the tenant
  comes from the verified token, never from the request body; a function gets only the permissions it needs; secrets live in a
  secret store. Full rules in [Security baseline](07-security-baseline.md).
- **Observable by default.** Every request has an id, every log line is structured and free of personal data, every capability
  exposes health and metrics, every failure carries enough context to find its cause without reproducing it. Full rules in
  [Logging and observability](06-logging-observability.md).

Enforced by: architecture tests (authorization coverage, no PII in log templates, no wildcard IAM actions), the kit's middleware, and
cdk-nag on infrastructure.

---

## When principles conflict

They sometimes do. Decide in this order:

1. **Correctness and security.** Never trade these.
2. **Clarity (KISS, clean code).** Code is read many more times than it is written.
3. **DRY.** Merge duplicated _knowledge_; leave duplicated _shape_ alone until a third copy appears.
4. **Performance.** Only after measuring. We have a load test and a size budget for exactly that.

When you break a rule on purpose, write why next to it (a short comment on the _why_), and if the rule is a gate, follow the
[exception process](03-quality-gates.md#changing-a-gate).

---

## Summary: what enforces what

| Principle               | Automatic enforcement                                               | Left to review                                    |
| ----------------------- | ------------------------------------------------------------------- | ------------------------------------------------- |
| KISS                    | Nesting and complexity limits (to add)                              | Whether a design is the simplest that works       |
| DRY                     | Shared test helpers; duplicate-code report (advisory)               | Which duplication is knowledge and which is shape |
| YAGNI                   | None                                                                | Whether a feature is needed now                   |
| Single responsibility   | One type per file; layer rules                                      | Whether a class has "and" in its description      |
| Open/closed, Liskov     | Contract tests run against every implementation; mutation testing   | The shape of the abstraction                      |
| Interface segregation   | NSubstitute analyzers                                               | Interface size                                    |
| Dependency inversion    | Project references; architecture tests                              | Whether a port is well named                      |
| Clean code              | Naming, braces, `var`, formatting, XML docs, unused code, analyzers | Naming quality, comment quality                   |
| TDD                     | Coverage threshold, mutation threshold                              | That the test was written first                   |
| Parse at the edge       | A validator test per request; boundary-value tests                  | What belongs in a type                            |
| Immutability and purity | Analyzers; domain has no dependencies                               | Design                                            |
| Deterministic time      | `DateTime.Now` ban                                                  | None                                              |
| Secure and observable   | Authorization coverage, log-template, IAM and cdk-nag checks        | Threat thinking                                   |
