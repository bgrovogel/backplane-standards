# Coverage

> Coverage measures which lines and branches of our code ran while the tests ran. It finds code no test reaches. It does **not** say the tests are good: that is what mutation testing is for.

**Read time:** 10 min · **Version:** coverlet.collector 10.1.0 (.NET), Jest's built-in coverage (TypeScript), ReportGenerator (merging) · **Last verified:** 2026-11-03

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) ·
[How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) ·
[What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

**Code coverage** is the share of our code that was executed during a test run. It has two common forms:

- **Line coverage:** of the executable lines, how many ran at least once.
- **Branch coverage:** of the decision points (`if`, `?:`, `switch`, `&&`), how many _outcomes_ were taken. An `if` has two branches: true and false. Running the line with only `true` is half a branch.

## Why we use it

It is a cheap, fast **floor**: code with 0% coverage is certainly untested, and a falling percentage is an early warning that a change came without tests (our TDD rule says it should not). We gate at **85%** for both line and
branch coverage on the backend, and statements, branches, functions and lines on the frontend. It is a _floor_, not the goal; the goal is protection, which [Stryker](stryker.md) measures.

## The big idea

> **Coverage tells you what was _not_ tested. It cannot tell you what was tested _well_.**

## How it works inside

**.NET (coverlet).**

1. `coverlet.collector` is a **data collector**, a plug-in the .NET test host runs alongside the tests (`--collect:"XPlat Code Coverage"`).
2. Before the tests run, it **instruments** the compiled assemblies: it rewrites their intermediate language (IL) to insert a counter at every **sequence point** (a place a debugger could stop) and at every branch.
3. As the tests run, the counters increment.
4. When the run ends, the collector writes the counts to a `coverage.cobertura.xml` file (Cobertura is a common XML format for coverage).
5. **ReportGenerator** merges the files from all test projects (so Domain code covered by Application tests counts) and produces a summary (`Summary.json`) and an HTML report.
6. CI reads the summary with `jq` and fails below the threshold.

What gets measured is set by `tests/coverage.runsettings`: include the four production assemblies, exclude generated code and migrations (`ExcludeByAttribute`, `ExcludeByFile`), skip auto-properties.

**TypeScript (Jest).** Jest instruments the code as it transforms it (using Istanbul), counts statements, branches, functions and lines, and applies `coverageThreshold` itself: if any metric is below 85%, Jest exits with a failure.

```
  .NET:   tests run + collector rewrote IL with counters --> cobertura.xml per project --> ReportGenerator merge --> Summary.json --> jq gate (>= 85)
  Jest:   source instrumented during transform --> counters --> coverage report --> coverageThreshold gate (>= 85)
```

## How it fits with everything else

- It runs **with** the tests ([NUnit](nunit.md), [Jest](jest.md)), so it is free of extra effort but adds a little time.
- It complements **mutation testing** ([Stryker](stryker.md)): coverage = "ran", mutation = "would notice".
- It is gate **G-21** in tier 2 ([Quality gates](../03-quality-gates.md)).
- It excludes **generated code** and **migrations**, so the number measures code we wrote.

## An example from our code

`tests/coverage.runsettings`:

```xml
<Include>[MortgageAnalytics.Domain]*,[MortgageAnalytics.Application]*,[MortgageAnalytics.Api]*,[MortgageAnalytics.Infrastructure]*</Include>
<ExcludeByAttribute>GeneratedCodeAttribute,CompilerGeneratedAttribute,ExcludeFromCodeCoverageAttribute</ExcludeByAttribute>
<ExcludeByFile>**/*.g.cs,**/Migrations/**/*.cs</ExcludeByFile>
<SkipAutoProps>true</SkipAutoProps>
```

- `Include` limits measurement to our four assemblies.
- `ExcludeByAttribute` skips compiler-generated and explicitly excluded code.
- `ExcludeByFile` skips source-generated files and EF Core migrations (generated).
- `SkipAutoProps` ignores trivial `{ get; set; }` properties.

And `client/jest.config.js`:

```js
coverageThreshold: {
  global: { statements: 85, branches: 85, functions: 85, lines: 85 },
},
```

In CI (`.github/workflows/ci.yml`), `COVERAGE_THRESHOLD: 85` and a `jq` check require both `linecoverage` and `branchcoverage` to be at least 85, failing the job with "Backend coverage is below 85%".

## How to use it

```
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
reportgenerator -reports:"coverage/**/coverage.cobertura.xml" -targetdir:coverage/report -reporttypes:"Html;JsonSummary"
cd client && npm test         # Jest prints the table and enforces its threshold
```

Open `coverage/report/index.html` and sort by uncovered lines. For each uncovered line ask: should it be tested, or should it be deleted?

## The rules we follow

| Rule                                                                         | Enforced by                                                          |
| ---------------------------------------------------------------------------- | -------------------------------------------------------------------- |
| Backend line and branch at least 85%; frontend all four metrics at least 85% | CI `jq` check; `jest.config.js`                                      |
| Generated code and migrations are excluded, in one place                     | `coverage.runsettings`                                               |
| `ExcludeFromCodeCoverage` needs a written reason                             | Review                                                               |
| The threshold only rises                                                     | [Gate rule 6](../03-quality-gates.md#rules-that-apply-to-every-gate) |
| Never write a test only to touch a line                                      | Review; mutation testing                                             |

## What goes wrong

| Symptom                                                          | Cause                                                    | Fix                                  |
| ---------------------------------------------------------------- | -------------------------------------------------------- | ------------------------------------ |
| 100% coverage and a bug still shipped                            | Tests ran the code but asserted nothing useful           | Mutation testing; assertion rules    |
| Coverage drops when a test project is added                      | Reports not merged (each project's report counted alone) | Merge with ReportGenerator           |
| Domain code shows as uncovered but Application tests exercise it | A report per project instead of a merged one             | Merge                                |
| The number differs between machines                              | Different assemblies included, or generated code counted | Use the shared `runsettings`         |
| Coverage collection slows the tests a lot                        | Instrumenting everything                                 | Keep `Include` narrow                |
| A branch shows half-covered                                      | Only one outcome of an `if` was tested                   | Add the other case (a boundary test) |

## Terms

| Term                | Meaning                                                                                 |
| ------------------- | --------------------------------------------------------------------------------------- |
| **Instrumentation** | Inserting counters into code so its execution can be measured                           |
| **Sequence point**  | A place in compiled code that maps to a source line                                     |
| **Branch**          | One outcome of a decision                                                               |
| **Cobertura**       | A widely supported XML format for coverage results                                      |
| **Threshold**       | The minimum acceptable value; below it the build fails                                  |
| **IL**              | Intermediate language: what the C# compiler emits before it is turned into machine code |

## Further reading

- [coverlet](https://github.com/coverlet-coverage/coverlet)
- [ReportGenerator](https://reportgenerator.io/)
- [Jest: code coverage](https://jestjs.io/docs/configuration#collectcoverage-boolean)
