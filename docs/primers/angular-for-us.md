# Angular, for our purposes

> Angular is the framework our front end is built with. This primer is not a full course; it explains the five ideas you need to read any file in `client/`: components, templates, dependency injection, signals and routing, and how each is organized in our codebase.

**Read time:** 20 min · **Version:** Angular 22 (`client/package.json`); zone-based change detection with signals, as configured in `app.config.ts` · **Last verified:** 2026-11-03

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) ·
[How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) ·
[What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

**Angular** is a framework from Google for building single-page web applications in **TypeScript** (JavaScript with types). One page is loaded once; afterwards Angular changes what is on screen as the person acts and as data arrives, talking to the server through an API. It
supplies the structure (components, routing, forms, an HTTP client, dependency injection, a build tool) so we do not assemble those from separate libraries.

## Why we use it

It is a complete, opinionated framework with strong typing, a testing story, accessibility tooling (the `angular-eslint` template rules), and long-term support, which suits a platform that needs **one consistent way** to build every application's UI. Three of our five apps already use it. The alternative,
React, is a library you combine with many choices (state, routing, forms); more freedom, but more variation between applications, which is what we are trying to remove ([decision](../adr/0001-stack.md)).

## The big idea

> **The screen is a function of state.** Components describe what to show for the current state; when the state changes, Angular updates only what changed.

## How it works inside

**1. Components.** A **component** is a TypeScript class with a **template** (HTML plus Angular syntax) and styles. It is **standalone**: it lists what it uses in its own `imports`, with no separate module file. A component is placed in HTML by its **selector** (`<app-kind-icon>`).
Inputs come in through `input()`; events go out through `output()`.

**2. Templates.** Bindings connect the template to the class: `{{ value }}` shows a value, `[attr]="expr"` sets a property, `(click)="method()"` handles an event. **Control flow** blocks drive structure: `@if (...) { }`, `@for (item of items; track item.id) { }`, `@switch`. Templates are compiled ahead of time to JavaScript,
so errors such as a missing property fail the **build** (one reason the Angular build is its own gate; Jest does not catch them).

**3. Dependency injection.** Services are classes marked `@Injectable({ providedIn: 'root' })` (one instance for the app). A component or service gets one with `private readonly settings = inject(UserSettings)`. It is the same idea as on the backend ([dependency injection primer](dependency-injection.md)): ask for what you need,
and let the framework provide it, which lets a test provide a fake.

**4. Signals.** A **signal** is a value that **notifies whoever reads it when it changes**.

- `const count = signal(0)` creates one; `count()` reads it; `count.set(5)` or `count.update(n => n + 1)` writes.
- `computed(() => count() * 2)` derives a value; it **tracks automatically** which signals it read and recomputes only when one changes.
- `effect(() => ...)` runs side effects when the signals it reads change.
- In a template, `{{ count() }}` subscribes the template to the signal; when it changes, Angular updates that part of the screen.
  Our **stores** (classes in `core/state/`) hold signals privately and expose read-only ones; components read them and call methods.

**5. Change detection.** Angular must know _when_ to re-check the screen. Our app uses **zone.js**: a library that wraps the browser's asynchronous APIs (clicks, timers, network responses), so Angular is told after each one and then checks the components (`provideZoneChangeDetection({ eventCoalescing: true })` batches events). Components that
set `ChangeDetectionStrategy.OnPush` are re-checked only when their inputs or signals change, which is faster and more predictable.

**6. RxJS and the HTTP client.** For streams of events over time, Angular uses **RxJS** **Observables**. `HttpClient` returns one per request. Operators transform them: `debounce` waits for a pause, `switchMap` cancels the previous request when a new one arrives, `retry` repeats. `takeUntilDestroyed` stops listening when a component is
removed. **Interceptors** (functions in the HTTP pipeline) add behaviour to every request or response, such as noticing a 401.

**7. Routing.** `app.routes.ts` maps URLs to components. **Lazy loading** (`loadComponent: () => import('...')`) makes a page its own downloadable chunk, fetched when first visited, so the first load is small (and a size budget fails the build if it grows). **Guards** (`canActivate`) run before a route opens.

**8. The build.** `ng build` compiles templates and TypeScript, bundles with esbuild, and enforces **budgets** (initial download warning at 870 kB, error at 900 kB).

```
  user acts (click/type) --> template (event binding) --> component/store method --> signal.set(...) / http call
        --> signal changes --> every template or computed that read it is notified --> only that part of the DOM updates
  http response --> store sets a signal --> screen updates
```

## How it fits with everything else

- The **API service** in `core/services/` is the only code that knows a URL, and it uses the types of the **contract** ([OpenAPI primer](openapi-contract-testing.md), [Architecture](../02-architecture.md#the-frontend)).
- **Jest** tests components, stores and pure functions in a simulated browser ([primer](jest.md)); **Playwright** tests the real app ([primer](playwright.md)).
- **ESLint and Prettier** keep the code and templates consistent, and check accessibility rules ([primer](eslint-prettier-stylelint.md)).
- **axe-core** scans the rendered pages ([primer](axe-core.md)).
- **Server validation errors** arrive as a map keyed by field and are shown beside the fields ([Validation and errors](../05-validation-and-errors.md#the-frontend)).
- In the platform, the **UI kit** provides tokens, components and the app shell as Angular libraries ([Strategy](../00-strategy.md#what-we-standardize)).

## An example from our code

**A request that follows the user's typing**, `client/src/app/core/state/latest-request.ts`:

```ts
/** How long to wait after the last change before sending, so typing "375000" sends one request, not six. */
export const RECALCULATE_DELAY_MS = 400;
...
export interface LatestRequest<TRequest, TResponse> {
  readonly request: Signal<TRequest | null>;   // the latest input, set at once
  readonly result: Signal<TResponse | null>;   // the latest successful response
  readonly errors: Signal<string[]>;           // messages from the most recent failure
  readonly busy: Signal<boolean>;              // true from the first change until its answer arrives
  send(request: TRequest, now?: boolean): void;
}
```

It exposes **read-only signals** and one method. Inside, `debounce` waits for the typing to pause; `switchMap` **cancels an in-flight request when a newer one arrives**, so a slow response can never overwrite a fresher result; errors are caught per request so one failure does not end the stream; a 503 or 429 retries after the server's `Retry-After` (up to 5 s).
It is shared by the calculator and the refinance comparison (DRY).

**A small standalone component**, `client/src/app/features/history/kind-icon.component.ts`:

```ts
@Component({
  selector: 'app-kind-icon',
  host: { '[attr.data-kind]': 'kind()' },
  template: `
    <svg aria-hidden="true" focusable="false" viewBox="0 0 24 24" ...>
      @switch (kind()) {
        @case ('Purchase') {
          <path d="M3 11l9-7 9 7" />
          ...
        }
        @case ('Refinance') {
          ...
        }
        @default {
          ...
        }
      }
    </svg>
  `,
})
export class KindIconComponent {
  readonly kind = input.required<SavedCalculationKind>();
}
```

A decorative icon: `input()` makes `kind` a signal input; `@switch` picks the drawing; `aria-hidden="true"` hides the decoration from screen readers because the kind is also named in words beside it (an accessibility decision written in the markup).

**Wiring**, `client/src/app/app.config.ts`: `provideRouter(routes, withPreloading(...))`, `provideHttpClient(withFetch(), withInterceptors([sessionEndedInterceptor]))`, and `provideAppInitializer(...)` calls that start background syncs before the first page. The **routes** (`app.routes.ts`) load the calculator's overview in the first download and every other page lazily.

## How to use it

```
cd client
npm start                 # dev server on :4200, proxies /api to the API on :5080
npm run build             # production build with budgets
ng generate component features/x/y --standalone    # scaffold (then fit our conventions)
```

Rules of thumb when writing a component: keep it small; read state from a store; keep logic in plain functions; give every interactive element an accessible name; give every list and page loading, empty and error states; put every visible string through the translation function; use design tokens, never raw colours.

## The rules we follow

| Rule                                                                         | Enforced by                                                                                                               |
| ---------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| Standalone components, signals for state, new control flow in templates      | Review; Angular lint rules                                                                                                |
| `features` never import other `features`; components never call `HttpClient` | Import lint rule in [`frontend-presets`](../../src/frontend-presets/README.md) ([G-10](../03-quality-gates.md#the-gates)) |
| Every page has loading, empty and error states                               | e2e per page; review                                                                                                      |
| Every visible string is translated and every language is complete            | `translations.spec.ts`                                                                                                    |
| Template accessibility rules pass                                            | `angular-eslint` `templateAccessibility`                                                                                  |
| The initial bundle stays within budget                                       | `angular.json` budgets (G-30)                                                                                             |
| Pure logic lives in plain functions in their own files                       | Review                                                                                                                    |

## What goes wrong

| Symptom                                | Cause                                                                                | Fix                                                   |
| -------------------------------------- | ------------------------------------------------------------------------------------ | ----------------------------------------------------- |
| Jest passes but `ng build` fails       | A template references a missing property; Jest does not compile templates fully      | Always run the build and `ng lint` before pushing     |
| The screen does not update             | A plain field changed instead of a signal; or an OnPush component with mutated input | Use signals; replace objects instead of mutating them |
| A request storm while typing           | No debounce                                                                          | `debounce` and `switchMap`, as `latestRequest` does   |
| An old response overwrites a newer one | No cancellation of the earlier request                                               | `switchMap`                                           |
| A memory leak after navigating away    | A subscription never ended                                                           | `takeUntilDestroyed`, or the async/signal bridges     |
| First load grows over budget           | A large import in the initial route                                                  | Lazy-load the page; `source-map-explorer` to find it  |
| Layout jumps when data arrives         | The space was not reserved                                                           | Reserve the height; the layout-shift test catches it  |

## Terms

| Term                  | Meaning                                                                |
| --------------------- | ---------------------------------------------------------------------- |
| **Component**         | A class plus a template and styles that renders one part of the screen |
| **Template**          | The HTML with Angular syntax that a component renders                  |
| **Standalone**        | A component that declares its own imports, with no module              |
| **Signal**            | A value that notifies readers when it changes                          |
| **Computed**          | A signal derived automatically from other signals                      |
| **Change detection**  | Angular checking what changed so the screen can be updated             |
| **zone.js**           | A library that tells Angular when asynchronous work finishes           |
| **Observable / RxJS** | A stream of values over time and the library of operators for it       |
| **Interceptor**       | A function that runs on every HTTP request or response                 |
| **Guard**             | A function that decides whether a route may open                       |
| **Lazy loading**      | Downloading a page's code only when it is first needed                 |
| **Budget**            | A size limit the build enforces                                        |

## Further reading

- [Angular documentation](https://angular.dev/)
- [Signals](https://angular.dev/guide/signals)
- [Components](https://angular.dev/guide/components) and [templates](https://angular.dev/guide/templates)
- [RxJS](https://rxjs.dev/guide/overview)
