# WebApplicationFactory

> The tool that starts our whole API, real pipeline and all, inside a test, and lets the test send it requests without opening a network port. It is how we write integration tests of the API.

**Read time:** 15 min · **Version:** `Microsoft.AspNetCore.Mvc.Testing` 10.0.13 (`Directory.Packages.props`) · **Last verified:** 2026-11-03

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) ·
[How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) ·
[What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

`WebApplicationFactory<TEntryPoint>` is a class from ASP.NET Core's testing package. You give it your application's entry-point type (`Program`); it **builds and starts your real application in memory** and gives you an `HttpClient` that
talks to it. The request goes through your real middleware, routing, validation, endpoints, use cases and database code, exactly as in production, but no TCP port is opened and nothing is deployed.

## Why we use it

Unit tests prove each part; they cannot prove the parts are _wired_ correctly: a route exists, a filter is registered, an error becomes a 400 with the right body, headers are set, the clock is the right one. An integration test through
the real pipeline proves that. It is far faster and more reliable than driving a browser, so we write most "does the API do the right thing" tests here and keep Playwright for journeys.

## The big idea

> **Run the real application in the test process, and replace only its outside edges (database file, mail, clock) with test versions.**

## How it works inside

1. **The entry point.** The test project references the API project. For the factory to find the application, the API exposes its entry type: `public partial class Program;` at the end of `Program.cs`
   (the top-level program is compiled into a hidden class; this line makes it visible).
2. **Building the host.** `WebApplicationFactory<Program>` calls the application's **own startup code**, the same `Program.cs` that runs in production: it reads configuration, registers services, builds the pipeline. Nothing is re-created in the test.
3. **TestServer instead of Kestrel.** Normally the host listens on a network socket through the **Kestrel** web server. The factory swaps in **`TestServer`**, an in-memory implementation: `CreateClient()` returns an `HttpClient`
   whose message handler calls straight into the pipeline as an in-process function call. No sockets, no ports, no firewall prompts, and it is quick.
4. **Overriding.** Your subclass overrides `ConfigureWebHost(IWebHostBuilder builder)`. There you can:
   - **change settings** with `builder.UseSetting("Email:OutboxDirectory", ...)` (configuration overrides),
   - **set the environment** with `builder.UseEnvironment("Production")`,
   - **replace services** with `builder.ConfigureTestServices(services => ...)`. This runs **after** the application's own registrations, so a replacement wins ([dependency injection](dependency-injection.md)).
5. **One host per factory.** Each `new MortgageApiFactory()` builds a separate application with its own container, so tests can have their own database and clock. The host starts lazily on the first `CreateClient()` and stops on `Dispose`.
6. **Cookies and state.** The client keeps a cookie jar like a browser, so sign-in persists across calls in a test.
7. **What it is not.** It is not Kestrel, not HTTPS, not a proxy, not HTTP/2, and not the real network. Things that live in those layers (a Kestrel request-size limit, forwarded headers from a real proxy, TLS) need other tests: we simulate a proxy with a
   test header, and the Docker smoke test and Playwright cover the real server.

```
 test --- HttpClient (in-memory handler) ---> TestServer --> the app's REAL pipeline:
                                                              request-id middleware -> rate limits -> auth -> validation filter
                                                              -> endpoint -> use case -> domain -> EF Core -> SQLite file (per host)
                                              replaced for the test:  mail transport (a recorder), clock (frozen), settings
```

## How it fits with everything else

- It hosts the app whose services come from **dependency injection**, which is what makes the replacements possible ([primer](dependency-injection.md)).
- It runs inside **NUnit** ([primer](nunit.md)); replacements are often **NSubstitute** substitutes ([primer](nsubstitute.md)); the clock is a **FakeTimeProvider** ([primer](testing-time.md)).
- The database is a real **SQLite file** built by the real **EF Core migrations** ([primer](ef-core-and-testcontainers.md)).
- `ContractSnapshotTests` fetches `/swagger/v1/swagger.json` through it ([OpenAPI primer](openapi-contract-testing.md)).
- It tests the **error contract** and **logging** ([validation and errors](../05-validation-and-errors.md), [logging](../06-logging-observability.md)).
- It is gate **G-20** in tier 2.

## An example from our code

`tests/MortgageAnalytics.Api.IntegrationTests/Infrastructure/MortgageApiFactory.cs` (read 2026-11-03). The class declaration and the key overrides:

```csharp
public sealed class MortgageApiFactory(
    Action<IServiceCollection>? configureServices = null, string environment = "Production", bool captureMail = true,
    Dictionary<string, string>? settings = null)
    : WebApplicationFactory<Program>
{
    public ConcurrentQueue<EmailMessage> SentMail { get; } = new();
    public FakeTimeProvider Clock { get; } = TestClock.Create();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:Calculations", $"Data Source={_databasePath}");
        builder.UseSetting("RateLimiting:AccountPermitsPerMinute", "10000");
        builder.UseSetting("Rates:Enabled", "false");
        ...
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            ...
        });
    }
}
```

Reading it:

- It inherits `WebApplicationFactory<Program>`: this _is_ the real app.
- `environment = "Production"` is pinned so tests see the same error handling as a deployment (the Development environment shows detailed errors).
- Each host gets **its own SQLite file** in the temp folder, built by the real migrations and deleted on dispose, so tests cannot interfere.
- The rate limit is raised for the suite (the tests sign up from one address) and tested separately with its own host; the external rates survey is switched off so no test calls the internet.
- `ConfigureTestServices` puts the **frozen clock** into the container, and a mail substitute that records every message in `SentMail`.
- A test header `X-Test-Remote-Address` stands in for the client's IP address, because an in-memory request has none.

A sketch of how a test uses it (simplified; `ComputeLimitsTests` and `AccountEnumerationTests` are the real ones):

```csharp
using MortgageApiFactory host = new(settings: new() { ["RateLimiting:AccountPermitsPerMinute"] = "3" });
using HttpClient client = host.CreateClient();
// ... post four sign-ups and assert the fourth is 429
```

## How to use it

- Derive a test base class that creates a factory and a client per fixture (`ApiTestBase` in the repository), and dispose them.
- Use **real requests and real JSON**: `client.PostAsJsonAsync(url, body)`; assert the status code first, then the body.
- To test a failure, build a host with a **substitute that fails** (`HostWithMailDown()`).
- To test time, advance `host.Clock`; remember components with their own clocks ([testing time](testing-time.md)).
- To keep tests fast, share one host across a fixture where tests do not interfere; give a test its own host when it changes settings, time or data.

## The rules we follow

| Rule                                                                                 | Enforced by                  |
| ------------------------------------------------------------------------------------ | ---------------------------- |
| Pin the environment to Production unless the test needs Development                  | The factory's default        |
| Replace only edges: mail, clock, settings, databases; never the unit under test      | Review                       |
| Each host has its own database, cleaned up on dispose                                | The factory                  |
| A host is created per test or fixture; never a static shared host with mutable state | Review                       |
| Tests do not call the internet                                                       | Settings switch partners off |
| Assert the status first, then the body, with a message                               | Conventions tests            |

## What goes wrong

| Symptom                                                               | Cause                                             | Fix                                                    |
| --------------------------------------------------------------------- | ------------------------------------------------- | ------------------------------------------------------ |
| "Cannot find the entry point" / `Program` is inaccessible             | The API does not expose `Program`                 | Add `public partial class Program;`                    |
| A test uses the real mail server or database                          | The substitute was registered before the real one | Use `ConfigureTestServices`                            |
| Tests interfere with each other                                       | A shared database or static state                 | One host and database per test or fixture              |
| Sign-in fails from a certain date                                     | Two clocks disagree (cookie jar versus app)       | Align them ([testing time](testing-time.md))           |
| Everything passes but the real deployment fails on headers or proxies | TestServer is not Kestrel or a proxy              | Docker smoke test, Playwright, a proxy simulation test |
| The build fails because the DLL is locked                             | A running dev API holds `bin/`                    | Stop it, or build to another path                      |
| Slow suite                                                            | A new host per test with a large startup          | Share a host per fixture where safe                    |

## Terms

| Term                       | Meaning                                                            |
| -------------------------- | ------------------------------------------------------------------ |
| **Integration test**       | A test of several parts working together                           |
| **TestServer**             | An in-memory web server for tests                                  |
| **Kestrel**                | ASP.NET Core's real web server                                     |
| **Host**                   | The running application: its container, configuration and pipeline |
| **Pipeline**               | The ordered list of middleware a request passes through            |
| **Configuration override** | A setting supplied by the test instead of the file                 |

## Further reading

- [Integration tests in ASP.NET Core](https://learn.microsoft.com/aspnet/core/test/integration-tests)
- [Customize `WebApplicationFactory`](https://learn.microsoft.com/aspnet/core/test/integration-tests#customize-webapplicationfactory)
