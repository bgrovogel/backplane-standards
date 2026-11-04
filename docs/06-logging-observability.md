# 6. Logging and observability

> When something goes wrong in production we can find out what, where and why from the evidence the system left behind,
> without reproducing it and without ever having logged a person's private data.

**Read time:** 20 min · **Best for:** adding logging, metrics or an alarm; diagnosing a production problem ·
**Builds on:** [Principles](01-principles.md#secure-and-observable-by-default) ·
**Reference implementation:** `src/MortgageAnalytics.Api/Observability/`

**On this page:** [The evidence we keep](#the-evidence-we-keep) · [Logging rules](#logging-rules) · [Levels](#levels) ·
[What to log and what never to](#what-to-log-and-what-never-to) · [The request id](#the-request-id) · [Metrics](#metrics) ·
[Tracing](#tracing) · [Health, alarms and runbooks](#health-alarms-and-runbooks) · [Audit trail](#audit-trail) ·
[Finding a production problem](#finding-a-production-problem-a-walk-through) · [On Lambda](#on-lambda) · [Testing observability](#testing-observability) ·
[Open decisions](#open-decisions)

---

## The evidence we keep

Five kinds of evidence, each answering a different question. They are not interchangeable.

| Evidence                | Answers                                                | Kept for        | Where                                       |
| ----------------------- | ------------------------------------------------------ | --------------- | ------------------------------------------- |
| **Logs**                | What happened in this request or job, in order         | Weeks to months | CloudWatch Logs, structured JSON            |
| **Metrics**             | How is the system doing over time: rate, errors, speed | Months to years | CloudWatch metrics (embedded metric format) |
| **Traces**              | Where did the time go across several services          | Days to weeks   | OpenTelemetry and AWS X-Ray                 |
| **Health and canaries** | Is it up right now, from the outside                   | Live            | `/health`, scheduled synthetic checks       |
| **Audit trail**         | Who did what to which record, and when                 | Years           | The database, append-only                   |

A log line is not an audit record, and an audit record is not a metric. Each has its own rules below.

---

## Logging rules

1. **Structured, never concatenated.** A log call is a **message template with named placeholders**
   (`"{Method} {Path} responded {StatusCode} in {ElapsedMilliseconds} ms"`), not an interpolated string. The values become
   fields in JSON that can be filtered and summed. A conventions test fails on `$"..."` or `+` inside a log call (**new**; Hyvva's
   `LoggingConventionTests` is the model).
2. **Source-generated log methods.** Use `[LoggerMessage]` (as `RequestLoggingMiddleware` does): no boxing, no template parsing at
   runtime, and the compiler checks the placeholders match the arguments.
3. **Every line carries the request id** through a logging scope, so one failure can be followed across every line it caused.
4. **Log at the boundary, once.** An exception is logged where it is handled, with context, not at each layer on its way up.
5. **One summary line per request** at Information, with method, path (without the query string), status and elapsed time.
   Health checks and static files are logged at Debug so they do not drown the lines that matter.
6. **Time comes from `TimeProvider`**, so tests can assert on durations.
7. **Never log personal data or secrets.** See [below](#what-to-log-and-what-never-to). Log an **id**, resolve it to a person only at read time.
8. **Logs are for people and machines.** Messages are short sentences a stranger understands; fields are stable names that dashboards depend on.
9. **Retention is set, not defaulted.** Every log group has an explicit retention period in the infrastructure code
   (a CDK assertion fails otherwise; Hyvva's `LogRetentionDefaultsTests` is the model). Logs that live forever cost money and keep data we promised to delete.
10. **Volume is a cost.** Debug and trace logging are off in production by default and can be turned on for one function for one hour
    through configuration, without a deploy.

---

## Levels

| Level           | Use it for                                                                       | Example                                                             |
| --------------- | -------------------------------------------------------------------------------- | ------------------------------------------------------------------- |
| **Trace**       | Step-by-step detail while developing                                             | Each iteration of a loop                                            |
| **Debug**       | Detail useful when diagnosing; off in production by default                      | A cache hit; a health check                                         |
| **Information** | A normal, meaningful event                                                       | Request completed; an email was handed to the provider              |
| **Warning**     | Something unexpected that the system handled and a person may want to know about | A retry succeeded on the second try; a rate limit rejected a caller |
| **Error**       | An operation failed and was not completed; needs attention                       | An unhandled exception; the mail provider rejected a message        |
| **Critical**    | The application cannot continue                                                  | A required setting is missing at startup                            |

A 4xx caused by a client's bad request is **not** an Error; it is expected behaviour and at most Information. A 5xx is an Error.

---

## What to log and what never to

**Log:**

- request in and out (method, route template, status, duration, request id, tenant id, user id),
- every call to a partner (AWS, Stripe) with its duration and outcome, never its payload,
- decisions that change behaviour (rate limited, served from cache, feature flag value used),
- state changes that matter (an account created, a plan changed) as a short event, _and_ an audit record,
- failures with their context and the exception,
- a startup summary of the effective configuration (never the secrets), as `StartupLog` does.

**Never log:**

| Never                                                                 | Why                                                                                       |
| --------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| Email, phone, name, address, date of birth, income, any personal data | Privacy law, and logs are widely readable and kept                                        |
| Passwords, tokens, API keys, session ids, signing keys, card data     | A leaked log becomes a breach                                                             |
| Request or response **bodies**                                        | They contain the two lists above                                                          |
| **Query strings** of requests                                         | They can hold one-time codes from emailed links (`mortgage-analytics` logs the path only) |
| Full exception text from a partner without review                     | It can echo the payload                                                                   |
| Stack traces to the client                                            | They help an attacker; they go in the log, never the response                             |

How this is enforced, in layers, because redaction by hand fails:

1. **Habit:** log ids, not values. `UserId` is fine; `Email` is not.
2. **A redaction library in the kit** (a destructuring policy and a regex catalog for emails, phone numbers and card-like numbers) wraps the
   logger, as Hyvva and the-hive each wrote separately and the kit will provide once.
3. **A test** that scans log templates for placeholders with banned names (`{Email}`, `{Phone}`, `{Name}`, `{Password}`, `{Token}`), the model being Hyvva's `LogPiiPlaceholderTests`.
4. **A review question** in the definition of done: "what does this feature log?"

---

## The request id

Every request gets an id, and that id is the thread through everything.

- It is the caller's `X-Request-ID` **if it is short and made only of letters, digits and `-_.:`** (so a proxy's or client's logs line up
  with ours and nobody can forge a log line), otherwise a new random one.
- It is returned in the response's `X-Request-ID` header, placed in **every error body** as `requestId`, and on **every log line**
  written while the request runs.
- When one service calls another, it forwards the id (and the W3C `traceparent` header), so a journey across capabilities has one thread.

This is implemented today in `RequestLoggingMiddleware` and `RequestLogging`; the kit packages it so every application gets it
identically.

---

## Metrics

A metric answers "how is it doing" without reading a single log line.

**For every endpoint and every capability, the RED set:**

| Letter       | Metric              | Alarm when                                              |
| ------------ | ------------------- | ------------------------------------------------------- |
| **R**ate     | Requests per minute | Drops to zero when traffic is expected (it may be down) |
| **E**rrors   | Share of 5xx        | Above the service's objective for five minutes          |
| **D**uration | p50 and p95 latency | p95 above the objective                                 |

**For resources, the USE set** (utilization, saturation, errors): Lambda throttles and concurrent executions, queue depth and age of the
oldest message, dead-letter queue depth, database connections and CPU.

**Business metrics** matter as much: sign-ups, emails sent, email bounces and complaints, payments succeeded and failed, rejected requests
(`mortgage.requests.rejected`), cache hit ratio (`mortgage.analysis.cache`).

Implementation: .NET's built-in `System.Diagnostics.Metrics` (`Meter`, `Counter`), exactly as `AppMetrics` does, exported through
OpenTelemetry; on Lambda, the **embedded metric format** (a specially shaped JSON log line that CloudWatch turns into a metric) avoids
extra API calls.

Rules: metric names are lowercase dotted and stable; **labels have low cardinality** (route template, status class, never a user id or a
full URL, which would create millions of series and a large bill).

---

## Tracing

A trace follows one request across functions, queues and calls, showing where the time went.

- **OpenTelemetry** is the instrumentation, with the AWS distro exporting to **X-Ray**.
- ASP.NET Core, HttpClient, the AWS SDK and EF Core are instrumented by libraries, not by hand.
- Trace ids are stored in log lines so a log search and a trace view link to each other.
- Sampling: all errors, a small percentage of the rest (configured, to control cost).

---

## Health, alarms and runbooks

- **`/health`** answers "can I serve a request": the process is up and its database answers. Keep it cheap and unauthenticated; it reveals nothing.
- **A canary** (a scheduled script, as in the-hive's `booking-creation-canary.js`) performs a real journey against the real deployment
  every few minutes, from outside. It catches what `/health` cannot: DNS, certificates, a broken deploy.
- **Alarms exist for every deployed capability**, defined in its construct, and asserted by a CDK test (G-43): error rate, p95 latency,
  throttles, dead-letter queue not empty, queue age, and (for scheduled work) "did not run".
- **An alarm without a runbook is noise.** Each alarm links to a short runbook in `docs/runbooks/`: what it means, how to confirm,
  how to mitigate, who decides. The alarm's name is the runbook's file name.
- **Service level objectives (SLOs)** are written down per capability (for example, "99.5% of sign-in requests succeed in under 1 s over 30 days")
  and alarms derive from them. The-hive's `slo-alarms` test is the model.

---

## Audit trail

The audit trail is a different thing from a log, and it is a [capability](08-capability-modules.md), not a logging feature.

| Log                                         | Audit trail                                                                                                              |
| ------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| For engineers diagnosing                    | For people accountable for decisions: admins, auditors, the user                                                         |
| Free-form fields, may be sampled or dropped | Fixed schema, **never** dropped                                                                                          |
| Short retention                             | Long retention, set by policy                                                                                            |
| Must never hold personal data               | Holds **who** (an id), **what**, **which record**, **when**, **before and after** where needed                           |
| Deleted with the log group                  | **Append-only**; survives deletion of the user it describes (no foreign key to the user, as buybright's `AuditLog` does) |

Recording an audit event is **fire-and-forget but safe**: it catches its own errors and never fails the request it describes
(buybright's `logAudit` does this), and a test proves the event was written (the "side effect" rule).

---

## Finding a production problem: a walk-through

1. A person reports: "I saw an error saving my calculation, reference `9c1d7e2a…`." That is the `requestId` from the error.
2. Search the logs for `RequestId = 9c1d7e2a…`. Every line the request wrote appears, in order: the summary line shows `POST /api/v1/saved 500`, and
   an Error line shows the exception and the failing call.
3. The metrics dashboard shows whether this is one request or a spike; the trace shows the slow call.
4. The alarm that fired (or should have) links to the runbook; the fix is made test-first with a test that reproduces it.
5. The post-incident note records the cause and the missing signal, and the missing signal becomes a metric, an alarm or a test.

If any step in that chain is impossible, **that is a logging or observability defect** and gets fixed like a bug.

---

## On Lambda

- **Write JSON to standard output**; the platform ships it to CloudWatch. No log files, no agents.
- **Cold starts are measured** (a metric for the first invocation's duration) because they are the main serverless cost to users.
- **Log retention and metric namespaces are set in the construct**, not by hand in the console.
- **Context is rebuilt per invocation:** the request id, tenant and user come from the event, never from a static field that survives
  into the next request on a warm instance.
- **Evaluate AWS Lambda Powertools for .NET** (logging, metrics, tracing and an idempotency utility) **before** writing the kit's
  versions: buy-or-adopt beats build when a maintained library already does the job. The evaluation is a task in the [roadmap](11-roadmap.md).

---

## Testing observability

Observability is behaviour, so it is tested.

| What                                           | Test                                                        | Status               |
| ---------------------------------------------- | ----------------------------------------------------------- | -------------------- |
| One summary line per request, with the id      | `RequestLoggingTests` (integration)                         | Exists               |
| The id reaches the response and the error body | Integration test                                            | Exists               |
| A caller's id is kept only when it is safe     | `ARequestIdFromTheCaller_IsKept_WhenItIsSafe` (integration) | Exists               |
| No personal data in log templates              | Scan test over `{Placeholder}` names                        | **New**              |
| No interpolated strings in log calls           | Roslyn scan                                                 | **New**              |
| Metrics are emitted                            | Test with a `MeterListener`                                 | Partly               |
| Log retention is set on every group            | CDK assertion                                               | Elsewhere (Hyvva)    |
| Alarms exist per capability                    | CDK assertion                                               | Elsewhere (the-hive) |
| The canary works                               | It runs on a schedule and alarms on failure                 | Elsewhere (the-hive) |

---

## Open decisions

| Decision                                         | Options                                                                      | Recommendation                                                                  |
| ------------------------------------------------ | ---------------------------------------------------------------------------- | ------------------------------------------------------------------------------- |
| Error tracking beyond CloudWatch                 | Sentry (Hyvva uses it), or CloudWatch alarms only                            | Start with CloudWatch only; add Sentry when an app has users; the owner decides |
| Build the logging kit or adopt Lambda Powertools | Build from the two existing redactors, or adopt Powertools and add redaction | Evaluate Powertools first ([roadmap](11-roadmap.md))                            |
| Metrics export                                   | Embedded metric format, or OpenTelemetry to CloudWatch                       | EMF on Lambda for cost; OpenTelemetry API in code so it can change              |
