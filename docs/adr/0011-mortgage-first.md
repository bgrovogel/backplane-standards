# 0011. The standards are applied to `mortgage-analytics` first; no rewrites yet

**Status:** Accepted · **Date:** 2026-11-03 · **Decided by:** the owner

## Context

The first plan ([document 11](../11-roadmap.md)) packaged the standards into shared repositories and then adopted them, with later phases that moved existing applications onto the platform. The owner clarified the intent: the goal is to standardize the processes so they can be used anywhere, **no existing application is to be rewritten for now**,
and `mortgage-analytics`, the app currently in active work, should **follow the new standards first**.

## Decision

We will apply the standards to `mortgage-analytics` **inside its own repository**, working through the measured gaps in [document 12](../12-mortgage-first.md) test-first, one item per commit. Extraction into shared packages follows afterwards and is verified by deleting this app's own copy. No other application is changed.
The platform **name** is a package and repository prefix only and is **not blocking**; it is chosen when the first package is extracted.

## Alternatives considered

| Option                                  | Good                                                         | Costs                                                                  | Why not                                                       |
| --------------------------------------- | ------------------------------------------------------------ | ---------------------------------------------------------------------- | ------------------------------------------------------------- |
| Package the standards first, then adopt | One step produces reusable output                            | New repositories and pipelines before the rules are proved; CI minutes | Rules should survive real code before others must follow them |
| Standards in all five apps at once      | Uniform quickly                                              | Large, risky, and the other apps are idle or legacy                    | The owner said no rewrites yet                                |
| **Mortgage first, then extract**        | Proved on a real app; no new infrastructure; flat CI minutes | The shared packages arrive later                                       | **Chosen**                                                    |

## Consequences

- **Easier:** every rule is tested on 4,300 existing tests' worth of real code; the extraction later is a move, not a design.
- **Harder:** until extraction, rules live in this repository and the platform folder holds the documentation; some work (primers, MAP generation) is done once here and lifted later.
- **Changes to the plan:** [document 11](../11-roadmap.md) gains a "mortgage first" note; the platform-name and public-or-private decisions are no longer blocking; the adoption phase for other apps is "not now".
- **Revisit when:** a second application is started or an existing one is chosen for adoption.
