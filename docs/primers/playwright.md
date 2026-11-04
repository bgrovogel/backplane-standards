# Playwright

> Playwright drives a real browser the way a person does: it opens pages, clicks, types and reads what is on screen. It is how we prove whole journeys work, and it carries our accessibility, layout and responsive checks.

**Read time:** 20 min · **Version:** `@playwright/test` 1.64 (`client/package.json`) · **Last verified:** 2026-11-03

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) ·
[How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) ·
[What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

**Playwright** is a browser automation and end-to-end testing tool from Microsoft. A test script launches Chromium, Firefox or WebKit (Safari's engine), navigates to a page, performs actions, and asserts on what appears. The test runner (`@playwright/test`) adds parallel
execution, reports, retries, screenshots and **traces**.

## Why we use it

Unit and integration tests cannot see what a person sees: layout, focus, real browser events, the Content-Security-Policy the server sends, fonts, print, text zoom. End-to-end (e2e) tests prove the **whole system works together** on the **production build**, and a few of them are the cheapest way to check accessibility,
responsiveness and layout stability in a real engine. Compared with Selenium it is faster and, because of **auto-waiting**, far less flaky. Cypress runs inside the browser and is limited to Chromium-family and Firefox with different trade-offs.

## The big idea

> **Act like a person, and let the tool wait for the page, never you.** Describe intent (click "Save"), not mechanics, and assert on what is visible.

## How it works inside

1. **Browser control.** Playwright starts a browser process and talks to it over a **WebSocket** using the browser's debugging protocol (the Chrome DevTools Protocol for Chromium; patched builds of Firefox and WebKit expose a similar one). Commands like "click at this element" go to the browser; events come back.
2. **Isolation.** One **browser** hosts many **browser contexts**: each is like a fresh, separate profile (own cookies, storage, permissions) and is cheap to create. **Each test gets its own context**, so tests do not leak sign-ins or state to one another. A **page** is a tab in a context.
3. **Locators.** `page.getByRole('button', { name: 'Save' })` or `page.getByTestId('results')` returns a **locator**: a _recipe_ for finding an element, not the element. The recipe is re-evaluated on every use, so it survives the page re-rendering.
   Prefer role, label and text locators (they match how users and assistive technology find things); use `data-testid` for what has no good accessible name.
4. **Auto-waiting.** Before an action, Playwright runs **actionability checks**: the element is attached, visible, stable (not animating), enabled and receives pointer events at its location. It retries until they pass or the timeout expires. You never write `sleep(500)`.
5. **Web-first assertions.** `await expect(locator).toBeVisible()` **retries** until true or time runs out, unlike a one-shot check. `expect.poll(...)` does the same for custom conditions. Our rule: every `expect` has a message.
6. **Fixtures.** `test.extend({ ... })` defines named, composable setup (an account already signed in, the calculator opened). A test lists the fixtures it wants and receives them; setup and teardown run around the test automatically.
7. **Projects and parallelism.** The config defines **projects** (Chromium, Firefox, WebKit) and runs test files in parallel **workers**. With `fullyParallel`, tests within a file too.
8. **Web server.** `webServer` builds and starts the application before the tests (`npm run build && dotnet run ...`), waits for `/health`, and stops it afterwards (or reuses a running one locally).
9. **Evidence.** `trace: 'retain-on-failure'` records a **trace**: a timeline with a DOM snapshot before and after each action, the network, console and screenshots, openable in a viewer. A failed test is diagnosed by opening its trace, not by re-running it hoping.

```
   test file (Node)  --WebSocket/DevTools protocol-->  Chromium / Firefox / WebKit
        |                                               +-- context A (own cookies)  -> page -> your app on http://localhost:5090
        +-- fixtures: account, signedIn, calculator     +-- context B (isolated)     -> page
        +-- locators (recipes) + auto-wait + web-first expect
   afterwards: report, screenshots, traces for failures
```

## How it fits with everything else

- It tests the **running system** built by the pipeline (production build of Angular served by the API) ([How everything fits together](00-how-it-fits-together.md)).
- It hosts **axe-core** for accessibility scans ([primer](axe-core.md)) and the reader-loss, keyboard, layout-shift, touch-target, theme, language and print checks ([testing strategy](../04-testing-strategy.md#the-catalog)).
- It is the top of the diamond: **Jest** and the API integration tests carry the detail ([Jest](jest.md), [WebApplicationFactory](webapplicationfactory.md)).
- Its specs are scanned by **conventions tests** (every `expect` has a message) ([Roslyn primer](roslyn-conventions-tests.md)).
- Gates: **G-25** to **G-29**, tier 2 for Chromium and tier 3 or 4 for the other engines ([Quality gates](../03-quality-gates.md)).

## An example from our code

**The configuration** (`client/playwright.config.ts`) states the philosophy in comments:

```ts
// No retries: a flaky test should fail loudly and be fixed, not be hidden by a second attempt.
retries: 0,
use: { baseURL: APP_URL, trace: 'retain-on-failure', screenshot: 'only-on-failure' },
projects: [
  { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  { name: 'firefox', use: { ...devices['Desktop Firefox'] }, testMatch: CROSS_BROWSER },
  { name: 'webkit', use: { ...devices['Desktop Safari'] }, testMatch: CROSS_BROWSER },
],
```

Chromium runs everything (and is the only browser CI installs); Firefox and WebKit run only the specs that differ between engines (`npm run e2e:cross`).

**A fixture** (`client/e2e/fixtures.ts`): the `account` fixture creates a new user, confirms it with the link read from the dev mailbox, and signs in through the API (`page.request` shares the browser's cookies); `signedIn` opens the calculator after that. A test just asks for `signedIn`.
An **automatic** fixture, `withinContentSecurityPolicy`, fails any test where the browser blocked something under the app's Content-Security-Policy, so a change that only works without the policy cannot pass.

**A page object** (`client/e2e/calculator.page.ts`): tests say `calculator.fillLoan({ homePrice: 450000, ... })`; the selectors and the mechanics (collapsed sections, recalculating as you type) live in one class. A change to the page edits one file, not 40 tests.

**A web-first wait** (`client/e2e/accessibility.spec.ts`): `await expect(page.getByTestId('results'), 'the results have arrived').toHaveAttribute('aria-busy', 'false');` waits for the app's own "busy" signal rather than a time.

## How to use it

```
cd client
npm run e2e:install            # once: browsers
npm run e2e                    # Chromium; builds the app and starts the API
npm run e2e:cross              # Firefox and WebKit, 2 workers
npx playwright test e2e/accessibility.spec.ts -g "dark"          # one file, by title
npx playwright test --ui       # a time-travel UI
npx playwright show-trace test-results/<test>/trace.zip           # diagnose a failure
```

Writing a test: ask for the fixtures you need; act through the **page object**; assert with web-first `expect(locator, 'message')`; use `getByRole`/`getByTestId`; never use a fixed wait; keep each test independent (its own account via a unique email).

## The rules we follow

| Rule                                                                            | Enforced by                                                                        |
| ------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| Test the **production build** under the real security headers                   | `webServer` config; the CSP fixture                                                |
| No retries; a flaky test is investigated from its trace within a day            | `retries: 0`; [gate rule 3](../03-quality-gates.md#rules-that-apply-to-every-gate) |
| Every `expect` has a message                                                    | `EveryPlaywrightExpect_HasAMessage`                                                |
| Every spec takes its `test` from the fixtures                                   | `EverySpec_TakesItsTestFromTheFixtures`                                            |
| Selectors live in page objects; prefer role and label over CSS                  | Review                                                                             |
| No fixed sleeps                                                                 | Review; web-first assertions                                                       |
| A test creates its own data (unique email) and never depends on another test    | Fixtures                                                                           |
| Stop a locally running review server before the run (it locks the API's `bin/`) | Team practice                                                                      |

## What goes wrong

| Symptom                                              | Cause                                                                               | Fix                                                                                       |
| ---------------------------------------------------- | ----------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| A test is flaky                                      | A fixed wait, a CSS selector tied to layout, shared data, or a real race            | Open the trace; use web-first waits; isolate data; fix the race (it may be a product bug) |
| Many tests time out together                         | The machine is loaded (many browser windows open), or a stale server holds the port | Rerun with fewer `--workers`; stop the stale server by PID                                |
| "Element is not visible / intercepts pointer events" | An overlay, a collapsed section, an animation                                       | Open the section first (in the page object); wait for the state                           |
| Passes in Chromium, fails in WebKit                  | A real engine difference (layout, date input)                                       | Fix the code or the test deliberately; that is why cross-browser exists                   |
| A test passes but the page is broken in the browser  | The test ran against the dev server, not the build                                  | Test the production build                                                                 |
| Port 5090 already in use                             | A previous run's server is still alive                                              | Find its PID and stop it; do not kill all `dotnet` processes                              |
| Screenshots differ on another machine                | Fonts and rendering                                                                 | Avoid page-wide screenshot assertions                                                     |

## Terms

| Term                 | Meaning                                                     |
| -------------------- | ----------------------------------------------------------- |
| **End-to-end (e2e)** | A test of the whole system from the user's side             |
| **Locator**          | A re-evaluated recipe for finding an element                |
| **Auto-waiting**     | Playwright waiting for an element to be ready before acting |
| **Fixture**          | Named setup a test requests and receives                    |
| **Page object**      | A class holding one screen's selectors and actions          |
| **Context**          | An isolated browser profile                                 |
| **Trace**            | A recording of a test run for later diagnosis               |
| **Flaky**            | A test that sometimes fails without a code change           |

## Further reading

- [Playwright documentation](https://playwright.dev/docs/intro)
- [Best practices](https://playwright.dev/docs/best-practices)
- [Auto-waiting](https://playwright.dev/docs/actionability)
- [Trace viewer](https://playwright.dev/docs/trace-viewer)
