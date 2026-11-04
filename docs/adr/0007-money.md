# 0007. Money is `decimal` in code, minor units only at the processor

**Status:** Proposed · **Date:** 2026-11-03 · **Decided by:** to be accepted by the owner

## Context

`mortgage-analytics` computes with `decimal` and rounds in one place. Hyvva stores integer cents and bans `decimal` and `double` for money (its `MoneyTypeRatchetTests`). buybright's TypeScript math uses floating point and a flat PMI rate. Stripe amounts are integers in the currency's minor unit.
Floating point cannot represent most cent values exactly, and a calculator that is a cent off, or a ledger that drifts, is a defect.

## Decision

We will represent money in the **Domain** as a `Money` value type wrapping `decimal`, with **one named rounding function** and explicit currency where more than one is possible. Amounts cross the JSON boundary as numbers with at most two decimals (rates with up to four). Integer **minor units** are used **only at the
payment-processor boundary** (an adapter converts). **Floating point is never used for money.** The browser displays amounts the server computed; client-side arithmetic that affects an amount shown as a result is avoided or is documented and tested against the server (the reference app's "what-if" slider repeats the
payment formula in the browser in floating point, which is a known weak spot).

## Alternatives considered

| Option                                 | Good                                         | Costs                                                                   | Why not                                              |
| -------------------------------------- | -------------------------------------------- | ----------------------------------------------------------------------- | ---------------------------------------------------- |
| Integer cents everywhere               | Exact; matches the processor                 | Awkward for rates, percentages and interest maths (fractions of a cent) | Calculations need decimals before the final rounding |
| `double` / floating point              | Convenient                                   | Inexact; accumulates error                                              | Rejected outright                                    |
| `decimal` in domain, cents at the edge | Exact enough, readable; one conversion point | A conversion step at Stripe                                             | **Chosen**                                           |

## Consequences

- **Easier:** the domain reads like the arithmetic it models; the existing 4,300-test suite and the independent oracle (which uses Python's `decimal`) apply unchanged.
- **Harder:** a ratchet and an analyzer must ban `double` and `float` for money; Stripe adapters need conversion tests.
- **Revisit when:** a multi-currency product needs currency-aware minor-unit rules throughout.
