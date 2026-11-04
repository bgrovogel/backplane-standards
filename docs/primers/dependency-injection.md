# Dependency injection

> A class says what it needs in its constructor; a container builds those things and hands them over. It is the mechanism that lets us swap a real database for a
> fake in a test, and a real mail server for a recorder.

**Read time:** 12 min · **Version:** `Microsoft.Extensions.DependencyInjection` 10.0 (built into ASP.NET Core) · **Last verified:** 2026-11-03

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) ·
[How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) ·
[What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

**Dependency injection (DI)** is a way of building objects where an object does not create the things it depends on; it _receives_ them. A class that needs to send email
declares `IMailTransport transport` in its constructor. It does not write `new SmtpMailTransport()`. Something else, the **container**, decides which implementation to give it.

## Why we use it

- **Swapping.** In production the container gives the class the real SES sender; in a test it gives a recorder. The class's code is the same either way.
- **Dependency rule.** The Application layer can depend on the _interface_ `ISavedCalculationRepository` without knowing EF Core exists ([Architecture](../02-architecture.md#the-dependency-rule)).
- **Honesty.** A constructor listing eight dependencies shows that the class does too much. DI makes the smell visible.

Alternative: create dependencies inside the class (hard to test, hides coupling) or a global "service locator" that classes ask for things (hides what a class needs). We use neither.

## The big idea

> **A class declares what it needs; one place, the composition root, decides what it gets.**

## How it works inside

1. **Registration.** At startup, `Program.cs` tells the container what to build: "when someone asks for `IMailTransport`, build `SmtpMailTransport`". These calls are grouped into
   extension methods per layer, `AddApplication()` and `AddInfrastructure(...)`. This is the **composition root**, the only place that knows every layer.
2. **Resolution.** When a request needs, say, an endpoint's use case, the container looks at that class's **constructor**, finds the types of its parameters (using reflection, the
   ability of .NET code to inspect types at run time), builds each one (recursively, because those have constructors too), and passes them in. The result is a graph of objects.
3. **Lifetimes.** Each registration says how long an instance lives:

| Lifetime      | A new instance...               | Use for                                   | Danger                                             |
| ------------- | ------------------------------- | ----------------------------------------- | -------------------------------------------------- |
| **Singleton** | once, for the whole application | Stateless services, caches, configuration | Holding anything per-request (leaks between users) |
| **Scoped**    | once per request (per scope)    | `DbContext`, per-request state            | Injecting into a singleton (see below)             |
| **Transient** | every time it is asked for      | Light, stateless helpers                  | Expensive construction repeated                    |

4. **Validation at start.** With scope validation on (the default in Development), the container refuses to start if a singleton depends on a scoped service: a **captive
   dependency**, where a long-lived object quietly keeps a short-lived one alive.
5. **Tests replace registrations.** Because the container is just a list of registrations, a test can register a substitute _after_ the real one, and the later one wins.
   This is how `WebApplicationFactory` swaps the mail transport and the clock ([primer](webapplicationfactory.md)).

```
   Program.cs  --  AddApplication()  --  services.AddSingleton<IAmortizationCalculator, AmortizationCalculator>()
                \- AddInfrastructure() -- services.AddScoped<ISavedCalculationRepository, SavedCalculationRepository>()
                                                  |
   request arrives -> container builds:  Endpoint -> UseCase(ctor: IRepository, IClock) -> Repository(ctor: DbContext)
```

## How it fits with everything else

- **NSubstitute** makes the fakes that DI hands to the class under test ([primer](nsubstitute.md)).
- **WebApplicationFactory** replaces registrations to put fakes in the real application ([primer](webapplicationfactory.md)).
- **Clean Architecture's ports and adapters** are interfaces and implementations the container connects ([Architecture](../02-architecture.md)).
- A **DI convention test** checks every registered type can actually be built (`DependencyInjectionTests` in `MortgageAnalytics.Application.Tests`).

## An example from our code

`src/MortgageAnalytics.Application/DependencyInjection.cs` registers the application's validators:

```csharp
services.AddValidatorsFromAssemblyContaining<AmortizationRequestValidator>(ServiceLifetime.Singleton);
```

This one line scans the Application assembly, finds every class derived from `AbstractValidator<T>`, and registers it as a singleton, so a new validator needs no registration
of its own. Validators are stateless, which is what makes a singleton safe. A repository, by contrast, wraps a `DbContext` (one per request) and is registered **scoped**.

A class using it, in the shape the codebase uses (primary constructor):

```csharp
internal sealed partial class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger, TimeProvider clock)
```

The class names three things it needs: the next step in the pipeline, a logger and a clock. A test gives it a frozen `FakeTimeProvider`; production gives it the system clock.

## How to use it

- Depend on the **interface** (or abstract class) you own, not on a concrete class: `ISavedCalculationRepository`, `TimeProvider`.
- Register in the layer's `AddX()` method, never in `Program.cs` directly (except the host's own wiring).
- Choose the **shortest lifetime that is correct.** Stateless and thread-safe: singleton. Touches the database: scoped.
- Read configuration through a typed **options class** (`IOptions<T>`) rather than raw strings.

## The rules we follow

| Rule                                                                       | Enforced by                           |
| -------------------------------------------------------------------------- | ------------------------------------- |
| Constructor injection only; no service locator, no `new` of a collaborator | Review; an architecture test (to add) |
| A class with more than about four constructor dependencies is split        | Review; analyzer (candidate)          |
| Every registration can be built                                            | `DependencyInjectionTests`            |
| Registration lives in one extension method per layer                       | Review                                |
| Time, randomness and configuration are injected, not read directly         | `DateTime.Now` ban; options classes   |

## What goes wrong

| Symptom                                                               | Cause                                               | Fix                                                                |
| --------------------------------------------------------------------- | --------------------------------------------------- | ------------------------------------------------------------------ |
| "Unable to resolve service for type X while attempting to activate Y" | X was never registered                              | Add it in the layer's `AddX()`                                     |
| "Cannot consume scoped service from singleton"                        | A captive dependency                                | Make the consumer scoped, or inject a factory                      |
| One user sees another user's data                                     | Per-request data held in a singleton                | Make it scoped; never store request data in a field of a singleton |
| A test still talks to the real thing                                  | The substitute was registered _before_ the real one | Register after (`ConfigureTestServices` runs last)                 |
| A circular dependency                                                 | A needs B and B needs A                             | Extract the shared part into a third type                          |

## Terms

| Term                    | Meaning                                                                      |
| ----------------------- | ---------------------------------------------------------------------------- |
| **Container**           | The object that holds registrations and builds objects                       |
| **Composition root**    | The single place that wires the application together                         |
| **Lifetime**            | How long one built instance is kept: singleton, scoped or transient          |
| **Captive dependency**  | A long-lived object holding a short-lived one it should not outlive          |
| **Reflection**          | The ability of code to inspect types and call members at run time            |
| **Primary constructor** | C# syntax that puts a class's constructor parameters in its declaration line |

## Further reading

- [Dependency injection in .NET](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection)
- [Dependency injection guidelines](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection-guidelines)
- Mark Seemann, _Dependency Injection Principles, Practices, and Patterns_
