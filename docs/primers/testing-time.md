# Testing time

> `TimeProvider` is how our code asks "what time is it"; `FakeTimeProvider` is how a test answers. Together they make anything that depends on the date, an expiry or a
> schedule repeatable.

**Read time:** 10 min · **Version:** .NET 10 `TimeProvider`; `Microsoft.Extensions.TimeProvider.Testing` 10.10.0 · **Last verified:** 2026-11-03

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) ·
[How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) ·
[What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

`TimeProvider` is an abstract class built into .NET (from version 8) that is the single source of the current time, of elapsed-time measurement and of timers. In production we use
`TimeProvider.System`, which reads the real clock. In tests we use **`FakeTimeProvider`**, a version whose time **only moves when the test says so**.

## Why we use it

Code that calls `DateTime.Now` directly has two problems: tests cannot control it, and it returns _local_ time, which differs between a laptop and a server. Anything involving "has this expired?",
"is it a new month?", "send a reminder in three days" or "how long did this take?" becomes untestable, or worse, passes today and fails next month. Injecting the clock turns time into an ordinary input.

## The big idea

> **Time is an input, not a global.** Pass it in, and a test can set it, freeze it and move it forward.

## How it works inside

1. **The abstraction.** `TimeProvider` has `GetUtcNow()`, `GetTimestamp()` and `GetElapsedTime(...)` (for measuring durations), `LocalTimeZone`, and `CreateTimer(...)`.
2. **Production.** `TimeProvider.System` implements them with the operating system clock.
3. **Injection.** A class takes `TimeProvider` in its constructor ([dependency injection](dependency-injection.md)); the application registers `TimeProvider.System` as a default (`TryAddSingleton`), so a test can register its own first.
4. **The fake.** `FakeTimeProvider` keeps the "current time" in a field. `GetUtcNow()` returns it. `SetUtcNow(...)` and `Advance(TimeSpan)` change it.
5. **Timers.** A timer created through a `FakeTimeProvider` does not use a real thread. When the test calls `Advance`, the fake checks which timers are now due and **fires their callbacks immediately on the calling
   thread**. A "wait 30 minutes" test completes in microseconds.
6. **A boundary to remember.** Only code that goes through the `TimeProvider` is controlled. Anything that reads the real clock itself is not (for example, a cookie jar in the test HTTP client judging expiry).

```
  production:  class -> TimeProvider.System -> the operating system clock
  test:        class -> FakeTimeProvider (frozen at 2026-10-09 15:30 UTC) -> test calls Advance(30 days) -> class sees the new time
```

## How it fits with everything else

- The **`DateTime.Now` ban** is a ratchet architecture test ([primer](netarchtest-and-ratchets.md)), so time cannot sneak back in.
- **WebApplicationFactory** registers the fake clock in the API host ([primer](webapplicationfactory.md)).
- **Logging** measures request duration with `GetElapsedTime`, so a test can assert it ([logging](../06-logging-observability.md)).
- **Hyvva and the-hive** both follow the same rule; the-hive has its own `IClock` interface, which the platform replaces with `TimeProvider` so there is one way.

## An example from our code

`tests/MortgageAnalytics.Testing/TestClock.cs`:

```csharp
public static class TestClock
{
    /// <summary>The instant every test treats as now: 9 October 2026, 15:30 UTC.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 9, 15, 30, 0, TimeSpan.Zero);

    public static FakeTimeProvider Create(DateTimeOffset? now = null) => new(now ?? Now);
}
```

Every test treats the same moment as "now", so "the first payment defaults to the 1st of next month" is always 1 November 2026 in tests, whatever the real date.

A test that needs time to pass advances it: in the API tests, `host.Clock.Advance(TimeSpan.FromDays(30))` checks that a session or a pending sign-up expires.

**A real incident worth knowing.** `MortgageApiFactory` freezes the app's clock at 9 October. The test HTTP client's **cookie jar** judged a cookie's expiry by the _real_ clock, so a sign-in cookie
stamped with the frozen date looked already expired, and 59 tests failed, in CI only. The fix (in `MortgageApiFactory`) tells the cookie authentication to use `TimeProvider.System`:

```csharp
services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options => options.TimeProvider = TimeProvider.System);
```

The lesson: freezing the clock in one place does not freeze it everywhere; two clocks that disagree produce bugs that only appear on certain dates.

## How to use it

```csharp
// in the class
public sealed class SessionService(TimeProvider clock)
{
    public bool IsExpired(Session session) => session.ExpiresAt <= clock.GetUtcNow();
}

// in a test
FakeTimeProvider clock = TestClock.Create();
var service = new SessionService(clock);
Session session = new() { ExpiresAt = clock.GetUtcNow().AddDays(14) };

Assert.That(service.IsExpired(session), Is.False, "a fresh session is valid");
clock.Advance(TimeSpan.FromDays(15));
Assert.That(service.IsExpired(session), Is.True, "after 15 days a 14-day session has expired");
```

For "today's date" use one shared helper rather than repeating the conversion: `clock.Today()` in `MortgageAnalytics.Application/Services/ClockExtensions.cs` returns
`DateOnly.FromDateTime(clock.GetLocalNow().DateTime)`, and its comment explains the choice (local time, because UTC would jump a month early on the last evening of a month for US users). Decide UTC versus local deliberately, once.

## The rules we follow

| Rule                                                                                     | Enforced by                                                        |
| ---------------------------------------------------------------------------------------- | ------------------------------------------------------------------ |
| Never `DateTime.Now` in production code                                                  | Ratchet test (Hyvva's `DateTimeNowBanRatchetTests`, to generalize) |
| `DateTime.UtcNow` only for audit stamps and log lines with no logic that depends on them | Review                                                             |
| Inject `TimeProvider`; do not create your own `IClock`                                   | Review                                                             |
| Tests share one frozen starting moment (`TestClock.Now`)                                 | `MortgageAnalytics.Testing`                                        |
| Never `Thread.Sleep` or `Task.Delay` in a test to "wait for time"                        | Review; advance the fake instead                                   |

## What goes wrong

| Symptom                                  | Cause                                                                | Fix                                                                 |
| ---------------------------------------- | -------------------------------------------------------------------- | ------------------------------------------------------------------- |
| A test passes today and fails next month | Something reads the real clock                                       | Find it (search for `DateTime`, `Stopwatch`) and inject             |
| Sign-in or expiry tests fail only in CI  | A component with its own clock (cookie jar, token validator)         | Point it at the same provider, or at the real one on purpose        |
| "Yesterday" is wrong near midnight       | UTC versus local date confusion                                      | Decide the zone once; test at 23:59 and 00:01                       |
| A timer-based test never fires           | The test forgot to `Advance`, or the code used `Task.Delay` directly | Use `TimeProvider.CreateTimer` or `Task.Delay(delay, timeProvider)` |
| Durations in a log are always 0          | The fake does not move during the call                               | Advance inside the test, or accept 0 and test the format            |

## Terms

| Term             | Meaning                                                           |
| ---------------- | ----------------------------------------------------------------- |
| **UTC**          | Coordinated Universal Time; the same everywhere                   |
| **Local time**   | UTC plus the machine's time zone offset; differs between machines |
| **Frozen clock** | A clock that stays at one moment until told to move               |
| **Timer**        | A callback that runs after a delay or on an interval              |
| **Time bomb**    | A test or code path that works today and fails on a future date   |

## Further reading

- [TimeProvider (.NET API docs)](https://learn.microsoft.com/dotnet/api/system.timeprovider)
- [`FakeTimeProvider`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.time.testing.faketimeprovider)
