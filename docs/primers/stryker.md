# Stryker (mutation testing)

> Stryker makes small deliberate bugs in a copy of our code, one at a time, and checks whether our tests notice. It measures how well the tests would catch a real bug, which
> coverage cannot.

**Read time:** 15 min · **Version:** Stryker.NET (dotnet tool, see `.config/dotnet-tools.json`); `@stryker-mutator/core` and `jest-runner` 10 for TypeScript · **Last verified:** 2026-11-03

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) ·
[How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) ·
[What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

**Mutation testing** asks: _if the code were subtly wrong, would any test fail?_ **Stryker** is the tool that does it. It changes the code in small, mechanical ways, each change called a **mutant**
(`a < b` becomes `a <= b`, `x + y` becomes `x - y`, `true` becomes `false`, a statement is deleted), runs the tests against each mutant, and reports which mutants the tests caught.

- A mutant that makes a test fail is **killed**: good, the tests noticed.
- A mutant that no test notices **survives**: bad, the tests cannot tell the wrong code from the right code.
- The **mutation score** is killed mutants divided by all valid mutants, as a percentage.

## Why we use it

**Coverage** only says a line _ran_ during the tests. A test that calls a method and asserts nothing gives 100% coverage and protects nothing. Mutation testing measures _protection_. It is the check that stops
tests being written "afterwards, to hit a number", because such tests tend to leave mutants alive.

It **adds no tests itself.** Each survivor is a diff, "this change did not break anything", and the fix is the test that would have failed. That makes it an excellent reviewer of our own TDD discipline.

Alternative: trust coverage and review (cheap, misses weak assertions). Mutation testing is slow, so we run it where it pays most: the Domain (the business rules), and on the nightly and release tiers for the rest.

## The big idea

> **Break the code on purpose; the tests are only as good as the breakages they catch.**

## How it works inside

1. **Analyse.** Stryker.NET uses **Roslyn** ([primer](roslyn-conventions-tests.md)) to parse the project's source and finds every place a **mutator** applies. Mutators are the rules for changes:
   arithmetic (`+` to `-`), equality (`==` to `!=`), relational (`<` to `<=`, `<` to `>`), logical (`&&` to `||`), boolean literals, string literals (to `""`), removing a statement, replacing a return value, and others.
2. **Instrument once.** Instead of compiling hundreds of copies, Stryker compiles the project **once**, with **every** mutation present but switched off, each guarded by a check on a variable that says which mutant is
   active (`if (activeMutant == 17) { a <= b } else { a < b }`). Switching mutants is changing one environment variable, so no recompiling is needed per mutant.
3. **Baseline.** Stryker runs the whole test suite once **without** a mutation, measuring which tests cover which lines (**coverage analysis**, per test). A failing baseline stops everything.
4. **Run each mutant.** For each mutant, Stryker activates it and runs **only the tests that cover that line** (from step 3), not the whole suite. That is the main speed-up.
5. **Classify.** The result for each mutant:

| Status            | Meaning                                                  | Counted in the score as |
| ----------------- | -------------------------------------------------------- | ----------------------- |
| **Killed**        | A test failed                                            | caught                  |
| **Timeout**       | The mutant made the code loop forever; the run timed out | caught                  |
| **Survived**      | All covering tests passed                                | missed                  |
| **No coverage**   | No test runs that line at all                            | missed                  |
| **Compile error** | The mutation did not compile                             | ignored                 |
| **Ignored**       | Excluded by configuration                                | ignored                 |

6. **Report.** An HTML report shows each file with its mutants and, for each survivor, the exact before and after code. The JSON report feeds thresholds.
7. **Thresholds.** `thresholds.break` fails the run (and the CI job) below a score; `high` and `low` colour the report.

```
   source --Roslyn--> find mutation sites --> compile ONCE with all mutants switched off
        --> baseline run: which tests cover which lines
        --> for each mutant: switch it on, run only covering tests -> killed / survived / timeout
        --> score = (killed + timeout) / (killed + timeout + survived + no coverage)  ->  report + threshold
```

For TypeScript, **StrykerJS** with the Jest runner does the same with a TypeScript mutator set (it instruments the source and runs the covering Jest tests).

## How it fits with everything else

- It needs a green, reasonably fast **NUnit/Jest** suite ([NUnit](nunit.md), [Jest](jest.md)); a slow suite makes it slower by the number of mutants.
- It complements **coverage** ([primer](coverage.md)): coverage says what ran, mutation says what is protected. We keep both gates.
- It checks the **substitutes** ([NSubstitute](nsubstitute.md)): a `Received` assertion that is too loose leaves mutants alive.
- It is gate **G-22**, run at tier 3 or 4 ([Quality gates](../03-quality-gates.md)); the Domain run stays in CI because it takes about two minutes.
- The `.NET` run uses **Roslyn**, the same library behind the conventions tests.

## An example from our code

`tests/MortgageAnalytics.Domain.Tests/stryker-config.json`:

```json
{
  "stryker-config": {
    "project": "MortgageAnalytics.Domain.csproj",
    "target-framework": "net10.0",
    "reporters": ["progress", "html", "json"],
    "thresholds": { "high": 97, "low": 95, "break": 95 }
  }
}
```

- `project` is the code to mutate (the Domain), found relative to the test project.
- `thresholds.break: 95` fails the run below 95%; `high: 97` and `low: 95` colour the report.
- Run it with `cd tests/MortgageAnalytics.Domain.Tests && dotnet stryker`. It takes about two minutes and writes `StrykerOutput/<date>/reports/mutation-report.html`.

The Application project's config has `high 95, low 90, break 0` (it reports but does not fail yet); the Angular logic's `stryker.config.json` has `high 95, low 90, break null`. In October 2026 the Domain score had slipped
from the quoted 98.3% to 95.7%, barely above the 95 break; 22 new `GuardLimitTests` rows brought it to 97.6%. That is the workflow: **a drop in score is found, each survivor read, the missing test written.**

A worked survivor: the code `if (paymentNumber < 1) throw ...` mutates to `<= 1`. If no test passes `1`, the mutant survives. The fix is a boundary test: payment number 1 is allowed, 0 is not. The mutation run found the
missing boundary; a human would have to guess.

## How to use it

```
dotnet tool restore
cd tests/MortgageAnalytics.Domain.Tests
dotnet stryker                          # whole project
dotnet stryker --since:main             # only code changed since main (fast, for a pull request)
```

In this repository one command does the same from the root, with the shared base configuration (`tools/mutation.json`) and the recording of the score ([mutation scores](../mutation-scores.md)):

```
python tools/mutation.py list                                  # the projects that can be mutated
python tools/mutation.py run Backplane.Conventions --record     # run it alone, then record the score (clean working tree only)
```

A project that references NUnit (a library of test fixtures, like `Backplane.Conventions`) is taken by Stryker for a test project and refused with "No project found"; setting `<IsTestProject>false</IsTestProject>` in its project file says it is not one.

Reading a survivor in the HTML report: open the file, find the red or orange mutant, read "replaced `<` with `<=`". Ask _what behaviour differs_, write a test that asserts it, rerun that file.
An **equivalent mutant** is one that cannot change behaviour (for example `i < n` to `i != n` when `i` only increments by one); exclude it with a comment (`// Stryker disable once ...: reason`) and a written reason.

## The rules we follow

| Rule                                                                                               | Enforced by                                                           |
| -------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| Domain breaks the build below 95%                                                                  | `stryker-config.json` and the CI job                                  |
| Application and logic projects are measured; their break threshold is raised once the score allows | Review; `docs/` records the last measured value                       |
| Every survivor is triaged: a test is written, or an exclusion has a written reason                 | Review                                                                |
| Mutation runs on the nightly or release tier, except the Domain                                    | [CI budget](../09-repositories-and-delivery.md#the-ci-minutes-budget) |
| A library in the kit is mutation tested like an application                                        | Library rule 5                                                        |

## What goes wrong

| Symptom                                           | Cause                                                          | Fix                                                  |
| ------------------------------------------------- | -------------------------------------------------------------- | ---------------------------------------------------- |
| The initial test run fails                        | The suite is red or depends on test order or on the real clock | Fix the baseline first                               |
| The run takes hours                               | A slow suite, or mutating a huge project                       | `--since`, mutate one project, speed up the tests    |
| Many "no coverage" mutants                        | Code no test reaches                                           | Write tests, or question why the code exists         |
| A score that jumps around                         | Flaky tests (a timeout is counted as caught)                   | Fix the flakiness                                    |
| A surviving mutant in logging or a message string | The test does not assert the text                              | Assert the message where it is part of the behaviour |
| Port-locked files during a run                    | A dev server holds `bin/`                                      | Stop it, or run in another folder                    |

## Terms

| Term                  | Meaning                                                                |
| --------------------- | ---------------------------------------------------------------------- |
| **Mutant**            | One deliberately changed copy of a piece of code                       |
| **Mutator**           | A rule for how to change code (swap `+` for `-`)                       |
| **Killed / survived** | A test failed (caught) / no test failed (missed)                       |
| **Mutation score**    | The percentage of mutants killed                                       |
| **Equivalent mutant** | A change that cannot alter behaviour, so no test can kill it           |
| **Coverage analysis** | Learning which tests execute which lines, to run only those per mutant |

## Further reading

- [Stryker.NET documentation](https://stryker-mutator.io/docs/stryker-net/introduction/)
- [StrykerJS documentation](https://stryker-mutator.io/docs/stryker-js/introduction/)
- [Mutation testing explained](https://stryker-mutator.io/docs/mutation-testing-elements/introduction/)
