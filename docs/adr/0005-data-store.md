# 0005. PostgreSQL through EF Core by default; DynamoDB for small operational state

**Status:** Proposed; **the owner should confirm, especially the idle-cost question** · **Date:** 2026-11-03 · **Decided by:** to be accepted by the owner

## Context

The five apps chose differently: `mortgage-analytics` SQLite or PostgreSQL through EF Core; Hyvva Aurora Serverless v2 (PostgreSQL with row-level security and EF Core); the-hive one shared DynamoDB table plus a second for operational state; Veranda and buybright
Neon PostgreSQL with Drizzle. The platform needs one default so capabilities and tests are uniform. Tenant isolation is the highest-stakes concern ([security baseline](../07-security-baseline.md#multi-tenancy)).

## Decision

We will use **PostgreSQL through EF Core** as the default store for business data, with **row-level security** as a second wall for tenant data, and **DynamoDB only for small operational state** (idempotency markers, rate-limit counters, circuit-breaker state), as the-hive does.
The hosting of PostgreSQL (Aurora Serverless v2, Neon, or another) is a separate choice decided on **idle cost** and operational effort.

## Alternatives considered

| Option                          | Good                                                                     | Costs                                                                                                                     | Why not (as the default)                                   |
| ------------------------------- | ------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------- |
| DynamoDB for everything         | Near-zero idle cost; scales to zero; no connection limits on Lambda      | Access patterns must be designed up front; no ad hoc queries or reporting; EF Core's testing and migrations story is lost | Hurts reporting and the existing EF Core tooling and tests |
| Aurora Serverless v2 PostgreSQL | AWS-native, RLS, EF Core, same account                                   | A cost floor while running; connection handling on Lambda (RDS Proxy)                                                     | Candidate hosting; the cost floor is the owner's call      |
| Neon PostgreSQL                 | Scale-to-zero, a free tier, a connection pooler; used by two of our apps | A third party outside the AWS account                                                                                     | Candidate hosting                                          |
| SQLite                          | Zero cost; fast tests                                                    | Single writer; not for Lambda; no RLS                                                                                     | Kept as the **test** engine only                           |

## Consequences

- **Easier:** one data layer for every capability and every test (SQLite for speed, PostgreSQL in Testcontainers for fidelity, [primer](../primers/ef-core-and-testcontainers.md)); SQL reporting; row-level security.
- **Harder:** Lambda needs connection pooling (RDS Proxy or a provider pooler); migrations need a runner outside the request path (a migrator step or function, as Hyvva and buybright have); an idle cost floor applies unless a scale-to-zero host is chosen.
- **Question for the owner:** what idle cost per environment is acceptable? That decides Aurora versus Neon (or DynamoDB for a low-traffic product).
- **Revisit when:** a product needs a different access pattern at scale, or idle cost dominates.
