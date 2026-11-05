# 0008. The standards of `mortgage-analytics` are the baseline

**Status:** Accepted · **Date:** 2026-11-03 · **Decided by:** the owner (who asked that new work follow the standards kept in `mortgage-analytics`, such as Roslyn conventions tests and Stryker, to keep quality up)

## Context

`mortgage-analytics` was built test-first with its standards enforced by the build: 15 Roslyn conventions tests, Stryker mutation testing (Domain gated at 95%), coverage gates at 85%, lint and format on every file, a contract snapshot, an independent oracle, about 4,320 automated tests and an accessibility-first
Playwright suite. Hyvva and the-hive added complementary practices: architecture ratchets, CDK stack tests, cdk-nag, canaries, nightly Lighthouse and dependency scans.

## Decision

We will take the reference app's standards as the **baseline** for every new application and capability, extend them with the practices above and with the additions in [Quality gates](../03-quality-gates.md) and [Testing strategy](../04-testing-strategy.md), package them in `backplane-standards`, and make
**every rule enforced by a machine or marked as a review rule**. A gate is relaxed only through the exception process.

## Alternatives considered

| Option                          | Good                  | Costs                                                   | Why not                                                       |
| ------------------------------- | --------------------- | ------------------------------------------------------- | ------------------------------------------------------------- |
| Looser standards for speed      | Faster first features | Quality debt compounds; the duplication problem returns | Contradicts the owner's goal of the best application possible |
| Start fresh with a new rule set | A clean slate         | Loses proven, working tooling                           | The tooling exists and works                                  |

## Consequences

- **Easier:** a new app starts with every gate on; the same rules apply everywhere; quality is not a matter of memory.
- **Harder:** a stricter first week for each new app; the standards need maintaining like a product; some gates (full mutation, cross-browser) cost time and run off CI.
- **Revisit when:** a gate's cost clearly exceeds the defects it prevents; record the evidence in an ADR.
