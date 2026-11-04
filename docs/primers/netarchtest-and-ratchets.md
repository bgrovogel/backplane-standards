# NetArchTest and ratchets

> Architecture tests check **what depends on what** and **what is banned**. A ratchet is an architecture test that counts violations and only ever lets the count fall, so a new rule
> can be adopted on old code without a big-bang cleanup.

**Read time:** 15 min · **Version:** `NetArchTest.Rules` 1.x (used in Hyvva); reflection and source scans (used in `mortgage-analytics`) · **Last verified:** 2026-11-03 against `Hyvva.ArchTests` and `mortgage-analytics`

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) ·
[How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) ·
[What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

An **architecture test** is an automated check of the _shape_ of the system rather than its behaviour: "the Domain never references Infrastructure", "no production code calls `DateTime.Now`", "every endpoint has an
authorization policy". There are four ways to write one, and we use all of them:

| Technique              | How it looks at the code                                         | Best for                                    | Used in                         |
| ---------------------- | ---------------------------------------------------------------- | ------------------------------------------- | ------------------------------- |
| **Project references** | The compiler refuses a reference the `.csproj` does not have     | The layer rule itself                       | All                             |
| **Reflection**         | Loads the assembly and inspects its types and members            | "No contract type exposes a domain type"    | `ContractLayeringTests`         |
| **NetArchTest**        | Reads the assembly's metadata and dependencies with a fluent API | "Core must not depend on Infrastructure"    | Hyvva `LayerDependencyTests`    |
| **Source scans**       | Reads source text or syntax trees                                | Bans on a construct (`DateTime.Now`, `any`) | Hyvva ratchets; our conventions |

## Why we use it

Clean Architecture's value disappears the first time someone, in a hurry, adds a `using` that points the wrong way. Review can miss it; a test never does. Architecture tests are the **fences** around the structure in
[Architecture](../02-architecture.md), and ratchets let us build the fence one section at a time.

## The big idea

> **If a structural rule matters, a test enforces it.** And if the rule is new and the code is old, the test enforces "not worse than today", then "better", then "zero".

## How it works inside

**NetArchTest** (a library, `NetArchTest.Rules`) reads a compiled assembly using **Mono.Cecil**, which parses the assembly file's metadata _without loading it into the running program_. From that it knows every type, and
every type that type refers to (base classes, fields, parameters, method calls). The fluent API filters types and asserts a condition:

```csharp
var result = Types.InAssembly(CoreAssembly)
    .ShouldNot().HaveDependencyOn("Hyvva.Infrastructure")
    .GetResult();

Assert.That(result.IsSuccessful, Is.True, $"Core references Infrastructure: {string.Join(", ", result.FailingTypeNames ?? [])}");
```

`GetResult()` returns whether the condition held and the names of the types that broke it.

**Reflection tests** (no extra library) load types with `typeof(X).Assembly.GetTypes()` and inspect `GetProperties()`, `GetReferencedAssemblies()` and so on. They are exact, and see what the compiler sees, but need the
assembly loaded.

**Source scans** walk the repository's files, apply a regular expression or a Roslyn parse, and collect matches ([Roslyn primer](roslyn-conventions-tests.md)).

**A ratchet** wraps any of these with a stored ceiling:

```
   first run:      found 42 violations  ->  write the ceiling "42"     (the test passes, but the number is now pinned)
   later change:   found 43              ->  FAIL  ("the count rose from 42 to 43: remove the new one")
   after a cleanup: found 30             ->  PASS, and lower the ceiling to 30 in the same change
   goal:           found 0               ->  the ceiling is 0, which is just a plain ban
```

Where a rule still has known violations, the ceiling lives in the test (a constant, or a list of allowed files), so lowering it is a visible change in the diff. Hyvva uses the pattern widely
(`DateTimeNowBanRatchetTests`, `ConsoleLogBanRatchetTests`, `DynamicKeywordBanRatchetTests`, `MoneyTypeRatchetTests`, `CdkIamWildcardActionsRatchetTests`, `ImgAltRatchetTests`,
`CrossFeatureImportRatchetTests` and more). Most of those lock in a count of **zero that was already reached**: the `DateTime.Now` test says "at HEAD the count is zero ... this ratchet locks that floor in".
The same test shape carries a higher stored ceiling for a rule that is still being adopted, which is the use this platform adds.

## How it fits with everything else

- They are **NUnit tests** ([primer](nunit.md)), run in tier 2 (G-09).
- They sit beside **conventions tests** ([primer](roslyn-conventions-tests.md)): conventions say how code is _written_, architecture tests say how it is _connected_ and what is _absent_.
- They back the structure rules in [Architecture](../02-architecture.md#how-each-rule-is-enforced) and the security rules in [Security baseline](../07-security-baseline.md).
- A ratchet at zero for `DateTime.Now` is what makes [testing time](testing-time.md) a rule rather than a habit.

## An example from our code

`mortgage-analytics` keeps one: `tests/MortgageAnalytics.Application.Tests/Architecture/ContractLayeringTests.cs` (reflection):

```csharp
List<string> leaks = contracts
    .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(propertyInfo => (Type: type, Property: propertyInfo)))
    .Where(exposure => UnderlyingTypes(exposure.Property.PropertyType).Any(type => type.Assembly == domain))
    .Select(exposure => $"{exposure.Type.Name}.{exposure.Property.Name} ({exposure.Property.PropertyType.Name})")
    .ToList();

Assert.That(leaks, Is.Empty, "contracts must use their own types; map domain types in AmortizationMapper:\n" + string.Join('\n', leaks));
```

It takes every public type in the `Contracts` namespace, looks at every property (including inside generic types like `List<LoanDto>`), and fails if any property's type lives in the Domain assembly. Why: if a contract used a domain
type, renaming that domain class would silently change the public JSON.

Hyvva's `tests/Hyvva.ArchTests/LayerDependencyTests.cs` shows the NetArchTest form (above), and `DateTimeNowBanRatchetTests.cs` shows a source-scan ban over `src/` and the CDK folder, with a long comment explaining _why_
the rule exists and what to use instead. That comment style is the standard: **a ban that does not say why gets worked around.**

## How to use it

To add an architecture rule:

1. State the rule and its **reason** in the test's summary comment.
2. Choose the technique: project reference first; then reflection or NetArchTest for dependencies; a source scan for a banned construct.
3. Write the test so its failure message lists each offender and says **what to do instead**.
4. Add a "scanner is alive" assertion for scans (it must have found files or types).
5. If existing code violates it and cannot be fixed now, store a ceiling (a ratchet) with a TODO and an owner, and lower it as you go.
6. Record the rule in [Architecture](../02-architecture.md#how-each-rule-is-enforced) or the security baseline.

## The rules we follow

| Rule                                                                    | Enforced by                                      |
| ----------------------------------------------------------------------- | ------------------------------------------------ |
| Domain depends on nothing; Application not on Infrastructure or Api     | Project references, plus a NetArchTest rule      |
| Contracts never expose domain types                                     | `ContractLayeringTests`                          |
| Cloud-agnostic layers take no AWS SDK dependency                        | `CoreHasNoAwsSdkDependencyTests` (to generalize) |
| No `DateTime.Now`, `dynamic`, `console.log`, `any`; money never `float` | Ratchets                                         |
| Every ratchet states its reason, its ceiling and how to lower it        | Review                                           |
| A ceiling only goes down                                                | Review of the diff                               |

## What goes wrong

| Symptom                                                   | Cause                                                   | Fix                                                         |
| --------------------------------------------------------- | ------------------------------------------------------- | ----------------------------------------------------------- |
| The test passes but the rule is broken                    | The assembly was not loaded, or the scan found no files | Anchor with a type from the assembly; assert the scan count |
| A ratchet ceiling quietly rises                           | Someone "fixed" a failing test by raising the number    | Review rule: a raised ceiling needs an ADR                  |
| False positives from a banned word in a comment or string | A regex on raw text                                     | Parse with Roslyn, or strip comments first                  |
| A reflection test fails to load an assembly               | Not yet loaded into the test's context                  | Reference one of its types to force the load                |
| The rule is right but nobody understands the failure      | No reason in the message                                | Write the why and the alternative into the message          |

## Terms

| Term           | Meaning                                                           |
| -------------- | ----------------------------------------------------------------- |
| **Assembly**   | A compiled `.dll` or `.exe`; one per project                      |
| **Metadata**   | The description of types and references stored inside an assembly |
| **Mono.Cecil** | A library that reads and writes assemblies without running them   |
| **Tripwire**   | A rule whose breach is a defect to fix, not a trade-off           |
| **Ratchet**    | A rule that may only get stricter                                 |
| **Offender**   | A type or file that breaks a rule                                 |

## Further reading

- [NetArchTest on GitHub](https://github.com/BenMorris/NetArchTest)
- Neal Ford et al., _Building Evolutionary Architectures_ (fitness functions: the general idea behind these tests)
