# 0006. One Lambda function per feature area, each a Minimal API

**Status:** Proposed; **to be proved by a spike** · **Date:** 2026-11-03 · **Decided by:** to be accepted by the owner

## Context

Serverless on Lambda needs a rule for how many functions an application becomes. The-hive uses one function per use case or route; Livenhood one function per service domain (about fourteen); Hyvva runs ASP.NET Core Minimal APIs with native AOT on Lambda; `mortgage-analytics` is one long-running API.
AWS has a managed `dotnet10` runtime. Cold starts, deployment size, permissions and local development all depend on the answer ([Architecture](../02-architecture.md#running-on-lambda)).

## Decision

We will deploy **one Lambda function per feature area (bounded context)**, each hosting an **ASP.NET Core Minimal API** behind API Gateway, so the same application code runs locally and on Lambda. A function's IAM role names only what its own area needs. The Domain,
Application and Infrastructure projects are unchanged; only the Api host differs. A spike with a real application (the mortgage calculation API) measures cold start with the managed runtime versus native AOT before this is accepted.

## Alternatives considered

| Option                         | Good                                | Costs                                                                 | Why not                                                |
| ------------------------------ | ----------------------------------- | --------------------------------------------------------------------- | ------------------------------------------------------ |
| One function per route         | Finest permissions and blast radius | Many artifacts and cold starts; shared code repeated; slow local runs | Too many moving parts for one person                   |
| One function for the whole app | Simplest                            | Biggest cold start; no isolation between features                     | Weak isolation                                         |
| Containers instead of Lambda   | No cold starts                      | Always-on cost; more to operate                                       | The owner chose serverless ([ADR 0001](0001-stack.md)) |

## Consequences

- **Easier:** few artifacts; shared middleware, validation and connection pool inside each function; local development is the same Minimal API.
- **Harder:** permissions are the union of a function's routes; cold start depends on the function's size, so areas are kept small.
- **Rules that follow:** shared code is compiled into each function, never a Lambda Layer; context is rebuilt per invocation, never held in static fields; no per-process cache or gate.
- **Revisit when:** the spike shows cold starts above the product's budget, or one feature area's permissions need to be narrower than its siblings'.
