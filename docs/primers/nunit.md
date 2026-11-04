# NUnit

> The framework that finds our C# tests, runs them, and reports which passed. It also gives us the assertion language (`Assert.That(actual, Is.EqualTo(expected))`).

**Read time:** 15 min · **Version:** NUnit 5.0.0, NUnit3TestAdapter 6.3.1, NUnit.Analyzers 4.15.0, Microsoft.NET.Test.Sdk 18.11.0 (`Directory.Packages.props`) · **Last verified:** 2026-11-03

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) ·
[How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) ·
[What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

**NUnit** is a unit testing framework for .NET. You write ordinary methods, mark them with attributes (`[Test]`), and NUnit discovers and runs them. It supplies the
**assertions** (statements that fail the test when false) and the helpers around them: parameterized tests, setup and teardown, and test categories.

## Why we use it

A test framework removes the plumbing so a test is only _arrange, act, assert_. We chose NUnit because its assertion model reads like a sentence, it has strong support for
parameterized tests (`[TestCase]`), it has analyzers that catch misuse at compile time, and three of our five apps already use it, so the standards carry over. xUnit and MSTest
do the same job with different conventions; the difference is taste and habit, and consistency across apps matters more than the choice.

## The big idea

> **A test is a method the framework finds by its attributes and calls for you. It passes if it finishes without an assertion failing or an exception.**

## How it works inside

1. **Compile.** Test code compiles to an ordinary assembly (`.dll`). `Microsoft.NET.Test.Sdk` makes the project runnable by the .NET test host, and `NUnit3TestAdapter` is the
   plug-in that lets the host speak to NUnit.
2. **Discovery.** `dotnet test` (or the IDE) starts the host, which loads the assembly. The adapter uses **reflection** (inspecting types at run time) to find every class that has methods marked
   `[Test]`, `[TestCase]` or `[TestCaseSource]`. No test is called by name anywhere in the code.
3. **Instantiation.** NUnit creates **one instance of the test class per fixture**, then calls each test method on it. (This is different from xUnit, which builds a new instance per test.
   In NUnit, a field set by one test is still there for the next unless a `[SetUp]` method resets it. See [what goes wrong](#what-goes-wrong).)
4. **Lifecycle.** The order for each fixture is: `[OneTimeSetUp]` once; then for each test `[SetUp]`, the test, `[TearDown]`; finally `[OneTimeTearDown]` once.
5. **Parameterized tests.** `[TestCase(3, 100)]` on a method with two parameters makes NUnit create one separate test **per attribute**, each with its own result. `[TestCaseSource(nameof(Cases))]` takes the rows
   from a property or method instead.
6. **Assertions.** `Assert.That(actual, constraint)` is the **constraint model**. `Is.EqualTo(5)`, `Is.Empty`, `Has.Count.EqualTo(3)`, `Throws.TypeOf<...>()` are _constraint objects_ that describe an expectation. `Assert.That`
   evaluates the constraint against the actual value and, on failure, throws an `AssertionException` whose message includes the expected value, the actual value and **our message**.
7. **Result.** The runner catches the exception (or none), records pass or fail, and prints the report. An unhandled exception is an error, distinct from a failed assertion.
8. **Multiple assertions.** `Assert.Multiple(() => { ... })` runs every assertion inside, collects all the failures, and reports them together, so the first failure does not hide the others.
9. **Parallelism.** By default tests run one at a time; `[Parallelizable]` lets fixtures or tests run on several threads. Tests must then not share mutable state.

```
  dotnet test  ->  test host loads Tests.dll  ->  NUnit adapter reflects over classes
               ->  finds [Test] / [TestCase] methods  ->  new FixtureClass()
               ->  [SetUp] -> test method -> [TearDown]  (per test)  ->  report
```

## How it fits with everything else

- Test projects get NUnit automatically from `tests/Directory.Build.props` ([MSBuild primer](msbuild-and-build-rules.md)).
- **NSubstitute** builds the stand-ins a test hands to the class under test ([primer](nsubstitute.md)); **FakeTimeProvider** freezes time ([primer](testing-time.md)).
- **WebApplicationFactory** hosts the API inside a NUnit test ([primer](webapplicationfactory.md)).
- **coverlet** measures coverage while NUnit runs ([primer](coverage.md)); **Stryker** reruns the NUnit tests against mutated code ([primer](stryker.md)).
- **Conventions tests** are themselves NUnit tests that read source files ([primer](roslyn-conventions-tests.md)).

## An example from our code

`tests/MortgageAnalytics.Domain.Tests/ExtraPayments/ExtraPaymentRuleTests.cs`:

```csharp
[TestFixture]
public class ExtraPaymentRuleTests
{
    [TestCase(1, 0)]
    [TestCase(2, 0)]
    [TestCase(3, 100)]
    [TestCase(300, 100)]
    public void Monthly_AppliesFromStartingPaymentOnward(int paymentNumber, decimal expected)
    {
        var rule = new MonthlyExtraPayment(100m, startingPaymentNumber: 3);

        Assert.That(rule.AmountFor(paymentNumber), Is.EqualTo(expected), $"payment {paymentNumber} with a monthly extra starting at payment 3");
    }
}
```

- `[TestFixture]` marks the class as a group of tests (optional in modern NUnit, kept for clarity).
- Four `[TestCase]` attributes make **four tests** from one method: payments 1 and 2 get no extra; payments 3 and 300 get 100.
- The method name reads `Subject_Condition_Outcome`: a monthly extra applies from its starting payment onward.
- _Arrange_ is the `new MonthlyExtraPayment(...)`; _act_ is `rule.AmountFor(paymentNumber)`; _assert_ is `Assert.That(..., Is.EqualTo(expected), message)`.
- The third argument is the **message**, which says what should be true and why. Our conventions test fails the build if it is missing.

## How to use it

```
dotnet test                                   # everything
dotnet test --filter "FullyQualifiedName~ExtraPaymentRuleTests"
dotnet test --filter "TestCategory!=Postgres" # skip tests marked [Category("Postgres")]
dotnet test --list-tests                      # list without running
```

Common constraints: `Is.EqualTo`, `Is.Not.Null`, `Is.Empty`, `Has.Count.EqualTo(n)`, `Is.EqualTo(x).Within(0.01)`, `Does.Contain("text")`, `Throws.TypeOf<DomainValidationException>()`,
`Is.InstanceOf<T>()`.

Several assertions in one test:

```csharp
Assert.Multiple(() =>
{
    Assert.That(result.Payment, Is.EqualTo(2387.00m), "principal and interest");
    Assert.That(result.Total, Is.EqualTo(3163.93m), "the whole monthly payment");
});
```

## The rules we follow

| Rule                                                                           | Enforced by                                                                     |
| ------------------------------------------------------------------------------ | ------------------------------------------------------------------------------- |
| Only the constraint model (`Assert.That`), never the classic `Assert.AreEqual` | `OnlyConstraintModelAssertionsAreUsed` (conventions)                            |
| Every `Assert.That` has a message                                              | `EveryAssertThat_HasAFailureMessage`                                            |
| More than one assertion in a test goes in `Assert.Multiple`                    | `MethodsWithSeveralAssertions_WrapThemInAssertMultiple`                         |
| One behaviour per test; name reads `Subject_Condition_Outcome`                 | Review ([testing strategy](../04-testing-strategy.md#rules-for-writing-a-test)) |
| No logic in a test; use `[TestCase]` for variants                              | Review                                                                          |
| Tests are deterministic: no real clock, network or unseeded random             | `DateTime.Now` ban; `FakeTimeProvider`; review                                  |
| NUnit analyzers are errors                                                     | The build (`NUnit.Analyzers`, `TreatWarningsAsErrors`)                          |

## What goes wrong

| Symptom                                                           | Cause                                                                              | Fix                                                          |
| ----------------------------------------------------------------- | ---------------------------------------------------------------------------------- | ------------------------------------------------------------ |
| A test passes alone and fails in the full run (or the reverse)    | Shared state: NUnit reuses one fixture instance, so a field survives between tests | Reset in `[SetUp]`, or build everything locally in the test  |
| `Assert.That(a, Is.EqualTo(b))` fails on decimals that look equal | Different scale or a floating-point type (`0.1 + 0.2`)                             | Use `decimal` for money; use `.Within(tolerance)` for floats |
| The failure message does not say what went wrong                  | No message, or a message that repeats the assertion                                | Say what _should_ be true and why                            |
| The test never runs                                               | Missing `[Test]`, a non-public method, a typo in a `[TestCaseSource]` name         | The analyzers usually catch it; run `--list-tests`           |
| A parameterized test shows one entry instead of many              | `[TestCaseSource]` rows lack distinct names                                        | Use `TestCaseData(...).SetName(...)`                         |
| A test hangs                                                      | `async void`, or a `Task` that is never awaited                                    | Return `Task`, `await` everything                            |

## Terms

| Term                   | Meaning                                                                               |
| ---------------------- | ------------------------------------------------------------------------------------- |
| **Assertion**          | A statement that must be true; a false one fails the test                             |
| **Constraint**         | An object describing an expectation (`Is.EqualTo(5)`)                                 |
| **Fixture**            | A class that groups tests                                                             |
| **Attribute**          | A label in square brackets on a class or method, readable by tools through reflection |
| **Reflection**         | The ability of code to inspect types and call members at run time                     |
| **Parameterized test** | One method run many times with different inputs                                       |
| **Test host**          | The process that loads the test assembly and runs it                                  |

## Further reading

- [NUnit documentation](https://docs.nunit.org/)
- [Constraint model](https://docs.nunit.org/articles/nunit/writing-tests/assertions/assertion-models/constraint.html)
- [Unit testing best practices in .NET](https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices)
