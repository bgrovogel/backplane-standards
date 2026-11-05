# Health checks

> A built-in ASP.NET Core feature: a `/health` address that runs small checks (can the app reach its database?) and answers 200 when all is well and 503 when it is not, so a machine can decide whether to send traffic, restart the app or alert a person.

**Read time:** 8 min · **Version:** ASP.NET Core 10 (`AddHealthChecks`, `MapHealthChecks`) and `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` 10.0.13 (`Directory.Packages.props`) · **Last verified:** 2026-11-04 against the repository's code; the official documentation was not re-read in this pass, so check a version-specific detail there before relying on it

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) · [How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) · [What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

A **health check** is a small piece of code that answers one question: "is this part of the app working?" The framework runs all registered checks, combines the answers into one **status** (`Healthy`, `Degraded` or `Unhealthy`), and serves it at an address. A **probe** is whatever asks that address: a container platform, a load balancer, an uptime monitor.

## Why we use it

A process can be running and useless: the database is gone, a disk is full, a key is missing. Without a health address, "is it up?" means "does the home page load?", which says nothing about the database and is slow. A health address is cheap to call every few seconds and tells the platform the truth, so it can wait for the app to be ready before sending traffic, restart it when it is stuck and report an outage before users do.

Alternatives: pinging the home page (too coarse and heavy); a custom endpoint (reinvents status codes, timeouts and aggregation); a full monitoring agent (a later step, see OpenTelemetry in the roadmap).

## The big idea

> **Each check returns a status. The endpoint returns the worst one as an HTTP status code: 200 if nothing is Unhealthy, 503 if anything is.**

## How it works inside

1. **Registering.** `services.AddHealthChecks()` adds the `HealthCheckService` and returns a builder. `.AddDbContextCheck<CalculationsDbContext>("database")` registers one check named `database`. That check asks Entity Framework Core whether the context can connect to its database (a cheap round trip), so it is Unhealthy if the database is unreachable.
2. **Mapping.** `app.MapHealthChecks("/health")` adds an endpoint. On a request it asks the service to run every registered check **in parallel**, each with a timeout, and waits for them all.
3. **Combining.** The overall status is the **worst** individual status: any Unhealthy makes it Unhealthy; otherwise any Degraded makes it Degraded; otherwise Healthy.
4. **Answering.** By default the response body is the status word as plain text (`Healthy`) and the HTTP code is **200** for Healthy and Degraded, **503** for Unhealthy. A failed check that throws is Unhealthy for that check, not a crash.
5. **Probing.** The platform calls the address on a schedule. The deployment compose file's check opens a socket and sends `GET /health`, passing only if the first line contains `200`. The compose `depends_on: condition: service_healthy` then makes the reverse proxy wait for the app.

```
   probe --GET /health--> endpoint --> run all checks in parallel --> worst status wins
                                                                     Healthy/Degraded -> 200 "Healthy"
                                                                     Unhealthy        -> 503 "Unhealthy"
```

## How it fits with everything else

- `AddHealthChecks().AddDbContextCheck<...>()` is in the Infrastructure layer (`AddInfrastructure`), because only it knows about the database; `MapHealthChecks("/health")` is in the host's `Program.cs`. The Application layer knows nothing of it, as the dependency rule says ([Architecture](../02-architecture.md)).
- The route is mapped **after** authentication and the rate limiter in the pipeline, but needs no sign-in. Host-filtering tests (`AllowedHostsTests`) use it as the simplest request to send.
- The deployment guide tells you to point an uptime monitor at it; the compose files use it for container health (`docs/12-deploying.md` in the reference repository, which is private).
- It is tested with `WebApplicationFactory`: the app starts in memory and `GET /health` must be `Healthy` ([WebApplicationFactory](webapplicationfactory.md)).
- Starting the app in a test also starts the background backup, which is why one test requests `/health` to be sure the host is running before it looks for the backup.

## An example from our code

`src/MortgageAnalytics.Infrastructure/DependencyInjection.cs`:

```csharp
services.AddHealthChecks().AddDbContextCheck<CalculationsDbContext>("database");
```

`src/MortgageAnalytics.Api/Program.cs`:

```csharp
app.MapHealthChecks("/health");
```

And the test, `tests/MortgageAnalytics.Api.IntegrationTests/PlatformEndpointTests.cs`:

```csharp
string body = await Client.GetStringAsync("/health");
Assert.That(body, Is.EqualTo("Healthy"), "the health endpoint is used by container orchestrators");
```

- One line registers the check; one maps the address. Everything else (timeouts, parallel runs, aggregation, the status code) is the framework's.
- The test asserts the body text, because that is what the platform's probe and a person with a browser see.

## How to use it

- Try it: run the API and open `http://localhost:5080/health`. Stop the database (or point the connection string at nothing) and it becomes `Unhealthy` with a 503.
- Add a check for something new (a mail server, a disk threshold): implement `IHealthCheck` (`CheckHealthAsync` returns `HealthCheckResult.Healthy()`, `.Degraded(...)` or `.Unhealthy(...)`), register it with `.AddCheck<T>("name")`, and write the test that makes it fail first.
- Separate **liveness** ("the process should be restarted if not") from **readiness** ("send me traffic now") by tagging checks and mapping two addresses with a predicate, when the platform needs both. We have one address today because we have one cheap check.

## The rules we follow

| Rule                                                                          | Enforced by                        |
| ----------------------------------------------------------------------------- | ---------------------------------- |
| `/health` needs no sign-in and reveals nothing but the status word            | Default writer; test               |
| A check is cheap and has a timeout, because it runs every few seconds         | Review                             |
| A check reports the app's own dependencies, not third-party sites             | Review                             |
| Every deployment has a probe that uses it                                     | Compose files; deployment guide    |
| A new dependency (queue, mail server) gets a check in the change that adds it | Review; the pull-request checklist |

## What goes wrong

| Symptom                                          | Cause                                                             | Fix                                                                         |
| ------------------------------------------------ | ----------------------------------------------------------------- | --------------------------------------------------------------------------- |
| The container restarts in a loop                 | The probe runs before the app is ready, or the check always fails | Give the probe a start period; read the check's description in the response |
| Healthy while users see errors                   | The check tests less than users need (only the database)          | Add the check that matches the failure                                      |
| The app is marked down because a partner is down | A check on a third-party service                                  | Make it `Degraded`, or leave it to monitoring                               |
| `/health` returns the SPA's index page           | The route is mapped after the static fallback                     | Map it before the fallback                                                  |
| A slow check makes every probe time out          | No timeout, or a heavy query                                      | Keep checks to a connection test; set a timeout                             |
| The probe cannot reach it behind a proxy         | Host filtering rejects the probe's `Host` header                  | Send the allowed host in the probe, as the compose check does               |

## Terms

| Term      | Meaning                                                  |
| --------- | -------------------------------------------------------- |
| Probe     | The thing that calls the health address                  |
| Liveness  | Is the process alive and not stuck? If not, restart it   |
| Readiness | Can it serve traffic now? If not, send traffic elsewhere |
| Degraded  | Working, but not fully; still answers 200                |

## Further reading

- Microsoft Learn: [Health checks in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks) (tags, predicates, custom response writers, the publisher).
- Kubernetes documentation on [liveness and readiness probes](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/) (the same idea on another platform).
- In this repo: `docs/12-deploying.md` (watching it) and `deploy/docker-compose.yml`.
