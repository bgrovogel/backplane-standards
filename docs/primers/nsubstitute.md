# NSubstitute

> The library that produces stand-ins for real collaborators in a test, so a test can say "when the mail server is down, this still works" without a mail server.
> This primer explains how it "magically" creates a working fake from an interface.

**Read time:** 15 min · **Version:** NSubstitute 6.2.0, NSubstitute.Analyzers.CSharp 1.0.17 (`Directory.Packages.props`) · **Last verified:** 2026-11-03

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) ·
[How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) ·
[What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

**NSubstitute** creates **substitutes** (also called mocks or test doubles) for interfaces and classes. A substitute looks like the real thing to the code under test, but
you control what it returns and you can ask it afterwards what was called and with what.

```csharp
IMailTransport transport = Substitute.For<IMailTransport>();   // a stand-in that implements the interface
transport.SendAsync(...).Returns(Task.CompletedTask);          // tell it what to answer
await transport.Received(1).SendAsync(message, token);         // ask it afterwards what happened
```

## Why we use it

A use case that sends email must be tested without sending email, and also **when sending fails**, which is hard to cause for real. A substitute can be told to fail on command. It also lets a
test **prove a side effect happened** ("the email was handed over, once, with these arguments"), which our [definition of done](../03-quality-gates.md#definition-of-done) requires.

We chose NSubstitute over Moq and FakeItEasy because its syntax is closest to plain C# (you call the substitute as if it were the real object), and its analyzers catch common mistakes at compile time.

## The big idea

> **A substitute is a class generated at run time that implements your interface and records every call made to it.** Configuring it means "when this exact call happens, answer that".

## How it works inside

This is the part that looks like magic. It is not.

1. **Generating the class.** `Substitute.For<IMailTransport>()` uses a library called **Castle DynamicProxy** to create, in memory and at run time, a brand-new class that implements `IMailTransport`. Each
   method of that class does not contain real logic; it forwards the call to a central object, the **call router**.
2. **Recording.** Every call to the substitute (method name, argument values) is stored in a list on the router, so it can be asked about later.
3. **Answering.** When a call comes in, the router looks for a configured answer that matches the call. If it finds none, it returns a default: `null` for most types, `0` for numbers, an **already-completed task** for
   `Task` methods, and another substitute for interfaces (so chains like `a.B.C` do not throw).
4. **Configuring, and the trick behind `Returns`.** In `transport.SendAsync(x, y).Returns(...)`, C# evaluates `transport.SendAsync(x, y)` **first**. That call goes to the router, which records it and
   remembers it as the **last call**. Then `.Returns(...)` is called on its result; `Returns` asks the router "what was the last call?" and attaches the answer to _that_ call's arguments. That is why the syntax looks
   like you are configuring a return value: you are making a call, then labelling it.
5. **Argument matching.** `Arg.Any<T>()` and `Arg.Is<T>(predicate)` work the same way: they push a _matcher_ onto a per-thread list, and return a dummy value so the call compiles. When the call reaches the router, it
   uses the queued matchers instead of comparing literal values. This is why you cannot mix `Arg.Any` with raw values in awkward ways, and why a matcher used outside a call causes a confusing error.
6. **Checking.** `Received(1).SendAsync(...)` switches the substitute into "verify" mode for the next call, then compares it with the recorded list and throws if the count does not match.
7. **Limits.** DynamicProxy can only override what is **overridable**: interface members, and class members marked `virtual` or `abstract`. A non-virtual method on a class runs the real code, not the substitute, and the
   configuration is silently ignored. NSubstitute's **analyzers** warn about exactly this at compile time.

```
 test code                 substitute (generated class)          call router
 ---------                 ----------------------------          -----------
 transport.SendAsync(m,t)  ---- forwards ---->                    records the call (the "last call")
        .Returns(task)     ---- asks router "last call?" ---->    attaches answer to that call's arguments
 sut.DoWork()  (uses it)   ---- forwards ---->                    finds a matching answer -> returns it; records the call
 transport.Received(1)...  ---- forwards ---->                    compares with the recorded list; throws if different
```

## How it fits with everything else

- The **class under test** receives the substitute through its constructor ([dependency injection](dependency-injection.md)).
- In integration tests, `WebApplicationFactory` **registers** a substitute in place of the real service ([primer](webapplicationfactory.md)).
- **NUnit** runs the test and reports a failed `Received` as a failure ([primer](nunit.md)).
- Our **testing strategy** says to substitute only **ports we own** ([doubles](../04-testing-strategy.md#test-doubles)); an SDK type is wrapped in a port first.
- **Mutation testing** shows whether a `Received` check really protects anything ([primer](stryker.md)).

## An example from our code

`tests/MortgageAnalytics.Api.IntegrationTests/AccountEmailFailureTests.cs`, lines 19 to 24 (as of 2026-11-03):

```csharp
private static MortgageApiFactory HostWithMailDown()
{
    IMailTransport transport = Substitute.For<IMailTransport>();
    transport.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new IOException("the mail server is down")));
    return new MortgageApiFactory(services => services.AddSingleton(transport));
}
```

Line by line:

- Line 21 creates the substitute: a generated class implementing `IMailTransport`. The interface has one method, `SendAsync`, so there is one thing to configure.
- Line 22 says: for **any** email message and **any** cancellation token, return a task that has already **failed** with an `IOException`. `Arg.Any` is the matcher from step 5.
- Line 23 starts the real API in memory and **registers the substitute** in place of the real transport. Everything else is the real application.

A test then signs up, and asserts that the account was still created although the mail failed. A second example in the same file uses `Arg.Do` to run code on each call:

```csharp
transport.SendAsync(Arg.Do<EmailMessage>(message => created!.SentMail.Enqueue(message)), Arg.Any<CancellationToken>())
    .Returns(_ => down ? Task.FromException(new IOException("the mail server is down")) : Task.CompletedTask);
```

`Arg.Do` captures each message into a list (a **spy**), and `Returns(_ => ...)` takes a function, so the answer can change during the test (mail works while the account is made, then "goes down").

## How to use it

```csharp
var repo = Substitute.For<ISavedCalculationRepository>();

repo.FindAsync(id, ownerId, token).Returns(saved);                     // an answer for a specific call
repo.FindAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Saved?)null);
repo.SaveAsync(Arg.Any<Saved>(), token).Returns(Task.FromException(new TimeoutException())); // failure

await repo.Received(1).SaveAsync(Arg.Is<Saved>(item => item.Name == "Home"), token);   // proved a side effect
await repo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), token);
```

For a method returning `Task<T>`, `Returns(value)` is enough (NSubstitute wraps it). To throw instead: `Returns(Task.FromException<T>(ex))` for async, or `.Throws(ex)` for sync.

## The rules we follow

| Rule                                                                                              | Enforced by                                                |
| ------------------------------------------------------------------------------------------------- | ---------------------------------------------------------- |
| Prefer a real object, then a fake, then a stub; a mock only for a side effect the test must prove | Review ([doubles](../04-testing-strategy.md#test-doubles)) |
| Substitute only interfaces we own (ports); wrap SDK types first                                   | Review; an architecture test (to add)                      |
| A shared fake, not a copy per test                                                                | Review                                                     |
| Use `Received` to prove side effects, with the exact arguments                                    | Definition of done, item 6                                 |
| NSubstitute analyzers are errors                                                                  | The build                                                  |
| Every `Assert.That` still has a message                                                           | Conventions test                                           |

## What goes wrong

| Symptom                                                              | Cause                                                                                | Fix                                                     |
| -------------------------------------------------------------------- | ------------------------------------------------------------------------------------ | ------------------------------------------------------- |
| `Returns` is ignored; the real code runs                             | Substituting a **non-virtual** member of a class                                     | Substitute an interface, or make the member `virtual`   |
| `CouldNotSetReturnDueToTypeMismatchException`                        | `Returns` after a call to something that is not a substitute, or a wrong return type | Check the call really is on a substitute                |
| `AmbiguousArgumentsException` / `UnexpectedArgumentMatcherException` | `Arg.Any` mixed with literals, or a matcher used outside a call                      | Use matchers for **all** arguments of one call, or none |
| A configured answer is used for the wrong call                       | The matcher is too broad (`Arg.Any`)                                                 | Match the specific values that matter                   |
| The test passes although the code is broken                          | The substitute answers everything with defaults, so nothing is really checked        | Assert on the outcome; check with a mutation run        |
| Tests pass individually and fail together                            | A substitute shared in a static field                                                | Create it per test                                      |

## Terms

| Term                         | Meaning                                                                                   |
| ---------------------------- | ----------------------------------------------------------------------------------------- |
| **Substitute**               | A generated stand-in for an interface or class                                            |
| **Mock / stub / spy / fake** | Kinds of test double; see the [table](../04-testing-strategy.md#test-doubles)             |
| **Proxy**                    | A generated object that stands in for another and forwards calls                          |
| **Castle DynamicProxy**      | The library that generates the proxy classes at run time                                  |
| **Call router**              | The object inside a substitute that records calls and picks answers                       |
| **Matcher**                  | `Arg.Any<T>()` or `Arg.Is<T>(...)`: a rule for which arguments a configuration applies to |
| **Side effect**              | An effect outside the return value: a message sent, a row written                         |

## Further reading

- [NSubstitute documentation](https://nsubstitute.github.io/)
- [Creating a substitute, setting return values, checking received calls](https://nsubstitute.github.io/help/getting-started/)
- Gerard Meszaros, _xUnit Test Patterns_ (the vocabulary of test doubles)
