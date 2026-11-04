# FluentValidation

> The library we use to describe, in readable code, what a valid request looks like, and to turn every violation into a clear message tied to a field. It is how "validate at the boundary" is implemented.

**Read time:** 12 min · **Version:** FluentValidation 12.2.0 and `.DependencyInjectionExtensions` 12.2.0 (`Directory.Packages.props`) · **Last verified:** 2026-11-03

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) ·
[How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) ·
[What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

**FluentValidation** is a .NET library for building validation rules with a fluent (chainable) syntax. You write a **validator** class for a request type; each line says what must be true of one property.
Running the validator returns a result listing every rule that failed, with the property name and a message.

## Why we use it

Validation written as `if` statements scattered through endpoints is easy to forget, hard to test and different every time. A validator is **one class per request**, readable like a specification, testable in isolation with no web server,
and reusable (shared rules become extension methods). It keeps validation **out of the domain and out of the endpoints**, where our architecture says it does not belong ([Architecture](../02-architecture.md)).

Alternatives: .NET's built-in data annotations (`[Range]`, `[Required]` attributes on the contract): simpler but weaker for cross-field rules and conditional logic, and they mix rules into the contract types. Hand-written checks: no structure.

## The big idea

> **A validator is a list of rules about one request type. Run it once, at the edge, and trust the request afterwards.**

## How it works inside

1. **Defining rules.** A validator inherits `AbstractValidator<T>`. In its constructor, `RuleFor(request => request.Rate)` takes an **expression tree** (the code `request => request.Rate` captured as data, not run). The library reads the tree to learn the property
   name (`Rate`), and builds a **rule** for it. Chained methods (`.NotNull()`, `.InclusiveBetween(0, 30)`, `.Must(...)`) add **validators** to that rule.
2. **Running.** `validator.ValidateAsync(request)` evaluates each rule in order. For each, it reads the property value (by compiling the expression into a fast delegate), runs the rule's validators, and for each failure creates a `ValidationFailure`
   (property name, message, attempted value, error code).
3. **Result.** The `ValidationResult` has `IsValid` and `Errors`. `ToDictionary()` groups messages by property name.
4. **Messages.** Each built-in validator has a default message with placeholders (`'{PropertyName}' must be between {From} and {To}`); `.WithName("Monthly extra payment")` changes the display name and `.WithMessage(...)` replaces the text.
5. **Conditions and children.** `.When(condition)` applies a rule conditionally; `RuleForEach(x => x.LumpSums).ChildRules(...)` validates each item in a collection, with indexed property names (`LumpSums[0].Amount`).
6. **Registration.** `AddValidatorsFromAssemblyContaining<T>()` scans an assembly, finds every `AbstractValidator<>` subclass and registers `IValidator<TRequest>` in the container ([dependency injection](dependency-injection.md)).
7. **The filter.** Our `ValidationFilter<TRequest>` is an **endpoint filter**: code that runs before an endpoint. It resolves `IValidator<TRequest>`, validates the request argument, and if invalid returns an **RFC 9457 validation problem** (400)
   without calling the endpoint. One generic filter serves every endpoint.

```
  request JSON --bind--> AmortizationRequest --> ValidationFilter<AmortizationRequest>
                                                    --> IValidator<AmortizationRequest>.ValidateAsync
                                                         --> rules read property values via compiled expressions
                                                    invalid -> 400 { errors: { "extraPayments.lumpSums[0].amount": [ "..." ] } }
                                                    valid   -> the endpoint runs
```

## How it fits with everything else

- It implements **validate at the edge** ([Validation and errors](../05-validation-and-errors.md)) and the error contract (RFC 9457 problem details).
- It lives in the **Application** layer (`Application/Validation/`), so the Domain stays free of packages ([Architecture](../02-architecture.md)).
- It is registered through **dependency injection** and run by an **endpoint filter** in the host.
- It is tested by **NUnit** unit tests at each rule's boundaries ([NUnit](nunit.md)), and **mutation tested** ([Stryker](stryker.md)).
- The **OpenAPI contract** can carry the limits (min, max) it enforces ([OpenAPI primer](openapi-contract-testing.md)).
- The **frontend** shows the same limits and displays the `errors` map beside the fields ([Angular primer](angular-for-us.md)).

## An example from our code

`src/MortgageAnalytics.Application/Validation/ExtraPaymentsValidator.cs`:

```csharp
internal sealed class ExtraPaymentsValidator : AbstractValidator<ExtraPaymentsRequest>
{
    public ExtraPaymentsValidator(decimal loanAmount, int termInMonths)
    {
        RuleFor(request => request.MonthlyAmount).ExtraAmount(loanAmount, allowZero: true).WithName("Monthly extra payment");
        RuleFor(request => request.MonthlyStartingPaymentNumber).PaymentNumberWithin(termInMonths).WithName("Monthly extra start");

        RuleFor(request => request.LumpSums)
            .NotNull()
            .Must(requests => requests.Count <= MaxLumpSums).WithMessage($"No more than {MaxLumpSums} lump sums are allowed.")
            .WithName("Lump sums");

        RuleForEach(request => request.LumpSums).ChildRules(lumpSum =>
        {
            lumpSum.RuleFor(request => request.Amount).ExtraAmount(loanAmount, allowZero: false).WithName("Lump sum");
            lumpSum.RuleFor(request => request.PaymentNumber).PaymentNumberWithin(termInMonths).WithName("Lump sum payment number");
        });
    }
}
```

- The validator takes the **loan amount and term** as constructor arguments, because the limits depend on them (an extra payment cannot exceed the loan; the payment number must be within the term). It is created by the parent request's validator, not
  by the container.
- `ExtraAmount(...)` and `PaymentNumberWithin(...)` are **shared rule extensions** (DRY): the same rule is used for the monthly, annual and lump-sum extras, in one place.
- `.WithName(...)` makes the message read "Monthly extra payment must be ..." for a person.
- A collection is validated item by item with `RuleForEach`, so the error for the third lump sum is keyed `extraPayments.lumpSums[2].amount`.

The generic filter that runs it is `src/MortgageAnalytics.Api/Filters/ValidationFilter.cs`; its last method converts `ExtraPayments.LumpSums[0].Amount` to `extraPayments.lumpSums[0].amount` so the error path matches the JSON body.

## How to use it

To add a field:

1. Write the failing validator test first (below).
2. Add `RuleFor(request => request.NewField)...` in the request's validator, or a shared extension if the rule recurs.
3. Use a **named constant** for the limit, owned by the Domain ([one source for each limit](../05-validation-and-errors.md#one-source-for-each-limit)).

A validator test uses the library's result object (illustrative: the names are simplified, and the real tests build requests with the builders in `MortgageAnalytics.Testing`):

```csharp
ValidationResult result = new AmortizationRequestValidator().Validate(AValid.Request() with { AnnualInterestRate = 30.01m });

Assert.Multiple(() =>
{
    Assert.That(result.IsValid, Is.False, "a rate above 30% is refused");
    Assert.That(result.Errors.Single().PropertyName, Is.EqualTo("AnnualInterestRate"), "the error names the field");
});
```

Test each rule below, at, and above its limit.

## The rules we follow

| Rule                                                                        | Enforced by                                               |
| --------------------------------------------------------------------------- | --------------------------------------------------------- |
| One validator per request type, in `Application/Validation/`, one file each | Conventions (one type per file); a presence test (to add) |
| A filter runs validators, so no endpoint can skip it                        | The generic `ValidationFilter`                            |
| Every limit is a named constant with one home                               | Review; a test per limit                                  |
| Messages are specific, safe and translatable (stable code beside text)      | Review; to add: codes                                     |
| Validators are stateless (so a singleton is safe)                           | Review                                                    |
| Every rule is tested at its boundaries                                      | Mutation testing (G-22)                                   |

## What goes wrong

| Symptom                                           | Cause                                                                  | Fix                                                                 |
| ------------------------------------------------- | ---------------------------------------------------------------------- | ------------------------------------------------------------------- |
| A rule never fires                                | A `.When(...)` condition that is never true, or a wrong property       | A test that fails first; mutation testing                           |
| The error key does not match the form field       | Property names are PascalCase, the JSON is camelCase                   | The filter converts paths; keep it                                  |
| A null child makes the validator throw            | A nested property accessed without a null check                        | `.When(request => request.Child is not null)`                       |
| The same limit is in two places and they disagree | Duplicated knowledge                                                   | One constant; one test                                              |
| A message leaks internal detail                   | A custom message with a value or an exception text                     | Keep messages about the rule                                        |
| Rules run in an unexpected order                  | Rules run in declaration order; cascade mode continues after a failure | Set `CascadeMode.Stop` where a later rule depends on an earlier one |

## Terms

| Term                | Meaning                                           |
| ------------------- | ------------------------------------------------- |
| **Validator**       | A class holding the rules for one type            |
| **Rule**            | A statement about one property                    |
| **Expression tree** | Code captured as data so a library can inspect it |
| **Endpoint filter** | Code that runs before or after an endpoint        |
| **Problem details** | A standard JSON error format (RFC 9457)           |
| **Boundary value**  | A value at the edge of what is allowed            |

## Further reading

- [FluentValidation documentation](https://docs.fluentvalidation.net/)
- [RFC 9457: Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc9457)
- [Minimal API filters](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/min-api-filters)
