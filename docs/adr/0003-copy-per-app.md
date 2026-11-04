# 0003. One copy per application by default

**Status:** Proposed · **Date:** 2026-11-03 · **Decided by:** to be accepted by the owner

## Context

A capability can run as **one shared deployment** used by many applications, or as **one deployment per application** built from the same versioned code. The first is cheaper to run and allows one sign-in across apps; the second isolates each app's data, outages, bills and upgrades.
The applications are for different products and possibly different customers, built and operated by one person.

## Decision

We will deploy **one copy of each capability per application** (in that application's AWS account and environment) unless the capability is _naturally shared_. Naturally shared capabilities, decided one by one, are: a **mortgage calculation** service that several apps call, and,
if the owner wants one sign-in across apps, a **user directory**. Everything else is per application.

## Alternatives considered

| Option             | Good                       | Costs                                                                                 | Why not (as the default)                                  |
| ------------------ | -------------------------- | ------------------------------------------------------------------------------------- | --------------------------------------------------------- |
| Everything shared  | Cheapest; one place to fix | Cross-app multi-tenancy to design and test; shared blast radius; all-at-once upgrades | A cross-tenant leak or an outage would span every product |
| Everything per app | Strong isolation           | Slightly higher cost and more deployments                                             | This is the default; only the shared exceptions differ    |

## Consequences

- **Easier:** a bug, a bill or an outage in one app cannot touch another; an app upgrades a capability when it is ready.
- **Harder:** a fix reaches an app only on its next upgrade; a report of capability versions per app is needed to see drift ([document 8](../08-capability-modules.md#versioning)); a small cost per deployment.
- **Revisit when:** idle cost per deployment becomes the dominant expense, or a real need for one sign-in across apps appears.
