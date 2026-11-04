# Jest

> The test runner for our TypeScript and Angular code. It runs hundreds of tests in a few seconds in a simulated browser, so front-end logic can be tested without opening a real one.

**Read time:** 12 min · **Version:** Jest 30.5, jest-preset-angular 17 (`client/package.json`) · **Last verified:** 2026-11-03

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) ·
[How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) ·
[What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

**Jest** is a JavaScript test runner and assertion library. You write `describe` blocks and `it` tests with `expect(...)` assertions; Jest finds files ending in `.spec.ts`, runs them in parallel worker processes, and prints results and coverage.
**jest-preset-angular** is the add-on that teaches Jest to compile Angular's TypeScript and templates and to set up Angular's testing environment.

## Why we use it

Front-end logic (stores, formatting, form behaviour, components) needs fast feedback. A real browser is slow; Jest runs the same code in a Node.js process against a **simulated DOM (jsdom)**, giving about 1,900 tests in around ten seconds. Jest has built-in mocking, fake timers, coverage and watch mode, and a huge ecosystem.
It cannot judge layout or real rendering, which is why Playwright exists beside it.

## The big idea

> **Test front-end behaviour in a simulated browser: fast, deterministic, and good for logic, but blind to real layout.**

## How it works inside

1. **Find.** Jest scans for files matching `testMatch` (`src/**/*.spec.ts`) and splits them across **worker processes**, one test file per worker at a time.
2. **Transform.** TypeScript and Angular templates are not understood by Node directly. jest-preset-angular **transforms** each file on the fly (TypeScript to JavaScript, and `templateUrl`/`styleUrl` references inlined) and caches the result.
3. **Environment.** Each test file runs in a **jsdom** environment: a JavaScript re-implementation of the browser's `document`, `window`, events and a subset of CSS. `setup-jest.ts` initializes Angular's test environment once per file.
4. **Registry and mocks.** Jest has its own module loader, so it can replace a module (`jest.mock('./x')`) or a function (`jest.fn()`) and record calls. `jest.useFakeTimers()` replaces timers so a 400 ms debounce can be advanced instantly.
5. **Run.** `describe`/`it` register tests; Jest runs them, awaiting promises; `expect(actual).toBe(expected)` throws a descriptive error on mismatch. `it.each([...])` makes one test per row.
6. **Angular.** `TestBed` builds a tiny Angular module for the test: it creates a component with its dependencies (real or fake), lets you set inputs, call `fixture.detectChanges()` to render, then inspect the DOM.
7. **Coverage.** With `collectCoverage` on, files are instrumented during the transform (Istanbul), and `coverageThreshold` makes Jest exit non-zero below 85%.

```
  jest -> find *.spec.ts -> worker per file -> transform TS/templates -> jsdom + Angular test setup
       -> describe/it run -> expect(...) -> pass/fail -> coverage gate
```

## How it fits with everything else

- It tests what **Playwright** cannot afford to: every formatting rule, store, guard and pipe, in milliseconds ([primer](playwright.md)); Playwright covers what jsdom cannot: layout, real events, the whole app.
- It type-checks poorly on its own: Jest transforms without full type-checking, so **`npm run typecheck`** (`tsc` on specs and e2e) is a separate gate ([ESLint, Prettier, stylelint primer](eslint-prettier-stylelint.md), G-07).
- **Stryker** with the Jest runner mutates the TypeScript logic ([primer](stryker.md)).
- **Coverage** is gate G-21 ([primer](coverage.md)).
- It tests Angular code ([Angular for us](angular-for-us.md)) and the translation dictionaries (`translations.spec.ts` fails when a language lacks a message).

## An example from our code

`client/src/app/core/formatting/duration.spec.ts`:

```ts
import { formatMonths, monthsInWords } from './duration';

describe('monthsInWords', () => {
  it.each([
    [0, '0 months'],
    [1, '1 month'],
    [12, '1 year'],
    [13, '1 year 1 month'],
    [298, '24 years 10 months'],
  ])('writes %i months as "%s", for a sentence', (months, expected) => {
    expect(monthsInWords(months)).toBe(expected);
  });
});
```

- `it.each([...])` is Jest's parameterized test: five rows, five tests, each titled by the template (`%i` the number, `%s` the string).
- The function under test is a **pure function** (no Angular, no DOM): the simplest and most valuable kind of test.
- The cases cover the singular/plural edge (`1 month`), the year rollover (`12`, `13`) and a large value (`298`).

For a component, the shape is: configure `TestBed` with the component, create it, set an input, `fixture.detectChanges()`, query the DOM, assert text or attributes, and assert a service was called through a `jest.fn()`.

## How to use it

```
cd client
npm test                                   # all tests with coverage
npm run test:watch                         # re-run what changed, no coverage
npx jest src/app/core/formatting           # one folder
npx jest -t "monthsInWords"                # by test name
npm run typecheck                          # strict compile of specs and e2e (not done by Jest)
```

Write the failing spec first; keep it small; use `it.each` for tables of cases; prefer **real** objects and plain functions to mocks.

## The rules we follow

| Rule                                                               | Enforced by                    |
| ------------------------------------------------------------------ | ------------------------------ |
| Coverage at least 85% on statements, branches, functions and lines | `jest.config.js` (G-21)        |
| Specs are type-checked strictly                                    | `npm run typecheck` (G-07)     |
| Pure logic is a plain function, tested without Angular             | Review                         |
| Every language has every message                                   | `translations.spec.ts`         |
| No real timers, network or clock in a spec                         | Review; fake timers, fake HTTP |
| Spec files sit beside the code (`x.ts`, `x.spec.ts`)               | Convention                     |
| Mutation score for logic at least 90 to 95                         | Stryker (G-22)                 |

## What goes wrong

| Symptom                                                     | Cause                                                                     | Fix                                                     |
| ----------------------------------------------------------- | ------------------------------------------------------------------------- | ------------------------------------------------------- |
| Jest passes but CI's typecheck fails                        | Jest does not fully type-check specs                                      | Run `npm run typecheck` before pushing                  |
| A test passes alone and fails in the suite                  | Shared state between tests (a service singleton, a module-level variable) | Reset in `beforeEach`; build fresh objects              |
| "Cannot find module" for a template or style                | Preset or transform not applied                                           | Check `jest.config.js` and `setup-jest.ts`              |
| A component test cannot see an element                      | `detectChanges()` not called, or the element renders after async work     | `fixture.detectChanges()`, `await fixture.whenStable()` |
| A time-based test is flaky                                  | Real timers                                                               | `jest.useFakeTimers()` and advance                      |
| jsdom does not support a feature (layout, `ResizeObserver`) | jsdom is not a browser                                                    | Test in Playwright, or stub the API                     |
| Coverage below the gate after a new file                    | No spec for it                                                            | Write the spec (first)                                  |

## Terms

| Term              | Meaning                                                                  |
| ----------------- | ------------------------------------------------------------------------ |
| **Spec**          | A test file (`*.spec.ts`)                                                |
| **jsdom**         | A JavaScript simulation of a browser's document and window               |
| **Mock / spy**    | A fake function that records how it was called (`jest.fn()`)             |
| **Transform**     | Converting source (TypeScript) into something Node can run               |
| **`TestBed`**     | Angular's helper for building a small module to test a component         |
| **Pure function** | A function whose result depends only on its inputs, with no side effects |

## Further reading

- [Jest documentation](https://jestjs.io/docs/getting-started)
- [jest-preset-angular](https://thymikee.github.io/jest-preset-angular/)
- [Angular testing guide](https://angular.dev/guide/testing)
