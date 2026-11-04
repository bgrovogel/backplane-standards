# 0002. Capability modules are the unit of reuse

**Status:** Proposed · **Date:** 2026-11-03 · **Decided by:** to be accepted by the owner

## Context

Every application needs sign-in, email, billing, an audit trail and so on. Reuse has three forms: a **library** (code compiled into an app), a **service** (a separate deployed program with its own data) and a **template** (a starting copy). A library alone
leaves each app to build the infrastructure and operations; a service alone leaves each app to write clients and UI. The owner's goal is that adding a capability to a new app is a few lines and "it all works".

## Decision

We will deliver each reusable feature as a **capability module**: an infrastructure construct, the service code, a typed client generated from an OpenAPI contract, optional UI pieces, a `Testing` package of fakes, and its documentation, threat model, SLO and
runbooks, versioned together with semantic versioning. A capability owns its data; applications use the client or its events. Libraries with no data or deployment (logging, clock, redaction) are `platform-kit` libraries, not capabilities.
A capability is extracted only when a second application needs it.

## Alternatives considered

| Option                                     | Good               | Costs                                                  | Why not                                |
| ------------------------------------------ | ------------------ | ------------------------------------------------------ | -------------------------------------- |
| Libraries only                             | Simple, in-process | Every app rebuilds infrastructure, data and operations | Does not deliver "inject and it works" |
| Shared running services only               | Deploy once        | Cross-app tenancy, shared outages, lock-step upgrades  | See [ADR 0003](0003-copy-per-app.md)   |
| Templates only                             | Easy to start      | Copies drift; no way to ship a fix                     | Drift is the problem we have today     |
| One microservice repository per capability | Strong isolation   | Many pipelines against a limited CI-minutes budget     | [ADR 0004](0004-repositories.md)       |

## Consequences

- **Easier:** a new app adds a capability in a few lines and inherits its tests, alarms and docs; a fix is one change and a version bump.
- **Harder:** each capability carries real work (contract, fakes, threat model, runbooks); contract compatibility must be policed (an OpenAPI diff in the release script); infrastructure upgrades can replace stateful resources, so they get dedicated tests.
- **Open:** how the service code is packaged ([ADR 0009](0009-capability-packaging.md)).
- **Revisit when:** more than a third of capabilities need heavy per-app forking, which would mean the boundaries are wrong.

## References

[Capability modules](../08-capability-modules.md), [Strategy](../00-strategy.md#the-idea-capability-modules).
