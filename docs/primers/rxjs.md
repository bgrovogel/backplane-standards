# RxJS

> A library for describing values that arrive over time (HTTP responses, keystrokes, timers) as streams you can combine and transform. Angular uses it for HTTP and form events; we use it where timing matters, and signals everywhere else.

**Read time:** 14 min · **Version:** `rxjs` ~7.8.0 (`client/package.json`) · **Last verified:** 2026-11-04 against the repository's code; the official documentation was not re-read in this pass, so check a version-specific detail there before relying on it

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) · [How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) · [What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

An **Observable** is a description of a sequence of values over time: zero, one or many, ending with completion or an error. Nothing happens until someone **subscribes**. **Operators** (`map`, `debounce`, `switchMap`...) are functions that take one observable and return a new one, so you build a **pipeline** by chaining them in `.pipe(...)`. A **Subject** is an observable you can push values into by hand, which makes it the bridge from ordinary code ("the user typed something") into a stream.

## Why we use it

Some problems are about **time**, and plain callbacks and promises are clumsy at them: "wait until the user stops typing for 400 ms", "if a newer request starts, cancel the old one", "retry twice, but only on these two errors, pausing as the server asks". Each is a line or two of operators and takes dozens of lines, with races, by hand.

Angular's `HttpClient` already returns observables, so RxJS is in the app whether we want it or not. Our choice is to use it **only where its strengths matter** (timing, cancellation, retry) and to turn the result into a **signal** for the rest of the code ([Angular primer](angular-for-us.md)).

Alternatives: promises and `async/await` (one value, cannot be cancelled, no operators for time); signals alone (hold a current value, but do not describe time or cancellation).

## The big idea

> **An observable is a recipe, not a value. Subscribing runs the recipe; unsubscribing stops it; operators change the recipe.**

Two consequences that explain most surprises: nothing runs until subscribed, and each subscriber usually gets its own run (an HTTP observable subscribed twice sends two requests).

## How it works inside

1. **Creating.** `new Observable(subscriber => { ... subscriber.next(1); subscriber.complete(); return teardown; })` is the root of everything. The function is the "recipe"; it runs on subscribe, and the function it returns runs on unsubscribe (clear a timer, abort a request).
2. **Operators are wrappers.** `source.pipe(map(f))` returns a new observable. When subscribed, it subscribes to `source` and calls `f` on each value before passing it on. A chain of operators is a chain of such wrappers; unsubscribing the outer one unsubscribes the inner ones.
3. **Flattening operators** are the ones to learn. When each value should start _another_ observable (a request per keystroke) there are four choices:
   - `switchMap`: **cancel** the previous inner observable, follow the newest. Right for "latest wins" (search, our calculator).
   - `mergeMap`: run all at once, results in any order.
   - `concatMap`: one at a time, in order.
   - `exhaustMap`: ignore new values while one is running (a Save button).
4. **Time operators.** `debounce`/`debounceTime` wait for a quiet period; `timer(ms)` emits after a delay; `retry({count, delay})` resubscribes on error, optionally after a delay that is itself an observable.
5. **Errors end the stream.** An error terminates the observable it reaches and everything downstream. To keep a long-lived stream alive, catch errors **inside** the inner observable (`catchError` after `switchMap`'s inner call, returning `EMPTY`).
6. **Cleaning up.** A subscription to a never-ending stream (a `Subject`, a form's `valueChanges`) leaks if you forget it. In Angular, `takeUntilDestroyed(destroyRef)` completes the stream when its component or service is destroyed. `toSignal(observable)` subscribes and exposes the latest value as a signal, unsubscribing automatically.

```
 user types -> Subject.next ->[debounce 400ms]->[switchMap: new request, cancel old]->[retry on 503/429]->[catchError]-> result signal
                                                         ^ the old request is aborted when a newer one starts
```

## How it fits with everything else

- **Angular** returns observables from `HttpClient`, `FormControl.valueChanges` and the router. `@angular/core/rxjs-interop` converts between observables and signals (`toSignal`, `toObservable`, `takeUntilDestroyed`) ([Angular primer](angular-for-us.md)).
- Our stores keep their state in **signals**; the one reusable pipeline, `latestRequest`, turns "the input changed" into "here is the latest result" and hides the RxJS inside it.
- Tests drive it with fake timers and `of(...)`/`throwError(...)` as stand-in API calls ([Jest primer](jest.md)); a mutation run checks the retry and debounce rules are really asserted ([Stryker](stryker.md)).
- The server side of the retry is the rate limiter and the compute gate, which answer 429 and 503 with `Retry-After` (`docs/04-backend.md` in the reference repository, which is private).

## An example from our code

`client/src/app/core/state/latest-request.ts` (the heart of it):

```ts
queue
  .pipe(
    debounce(({ now }) => (now ? of(0) : timer(RECALCULATE_DELAY_MS))),
    switchMap(({ request: next, id }) => {
      errors.set([]);
      return defer(() => call(next)).pipe(
        retry({ count: BUSY_RETRIES, delay: pauseBeforeAskingAgain }),
        map((response) => ({ response, id, answered: next })),
        catchError((error: unknown) => {
          errors.set(toErrorMessages(error));
          settle(id);
          return EMPTY;
        }),
      );
    }),
    takeUntilDestroyed(destroyRef),
  )
  .subscribe(({ response, id, answered }) => {
    result.set(response);
    settle(id);
    onAnswered?.(answered);
  });
```

- `queue` is a `Subject`: `send(request)` calls `queue.next(...)`, which is the only way values enter.
- `debounce` waits 400 ms after the last change, so typing "375000" sends one request, not six. The first request of a page goes at once (`of(0)` is a timer that fires immediately).
- `switchMap` starts the call and **cancels any call still in flight** when a newer change arrives, so a slow old answer can never overwrite a fresher result.
- `defer(() => call(next))` makes each retry a fresh call. Without `defer`, the same already-created observable would be re-run, which for some sources replays the old outcome.
- `retry({ count, delay })` asks again twice, but only after `pauseBeforeAskingAgain` agrees: a 503 or 429 waits as the server's `Retry-After` says (capped); any other error is passed on, because repeating a bad request only repeats the answer.
- `catchError` is **inside** the `switchMap`, so one failed request becomes a message and the outer stream keeps listening. Placed outside, the first failure would end the stream for good.
- `takeUntilDestroyed` ends the pipeline with its owner. The `subscribe` at the end writes into signals, which is where the rest of the app reads.

## How to use it

- **Prefer signals**; reach for RxJS when you need time (debounce, delay), cancellation (`switchMap`) or retry.
- Import operators from `'rxjs'` (RxJS 7.2+ exports them from the top level; the older `'rxjs/operators'` path still works).
- To see values while debugging add `tap((value) => console.log(value))` inside a pipe, and remove it before committing.
- Write the test first with fake timers: advance 399 ms and expect no call, advance one more and expect one.

## The rules we follow

| Rule                                                                                 | Enforced by                      |
| ------------------------------------------------------------------------------------ | -------------------------------- |
| State is in signals; a pipeline's last step writes to them                           | Review; the Angular primer       |
| Every subscription to a long-lived stream ends with the owner (`takeUntilDestroyed`) | Review                           |
| `catchError` goes inside the inner observable of a flattening operator               | Review; `latest-request.spec.ts` |
| A retry only repeats errors that repeating can fix                                   | `latest-request.spec.ts`         |
| "Latest wins" uses `switchMap`; a button that must not repeat uses `exhaustMap`      | Review                           |
| Each request/response race has a test that a slow old answer cannot win              | `latest-request.spec.ts`         |

## What goes wrong

| Symptom                                    | Cause                                                        | Fix                                                          |
| ------------------------------------------ | ------------------------------------------------------------ | ------------------------------------------------------------ |
| Nothing happens                            | The observable was never subscribed                          | Subscribe, or convert with `toSignal`                        |
| The request is sent twice                  | Two subscribers (a template and code) to one HTTP observable | Subscribe once and share the result in a signal              |
| A page keeps reacting after it is gone     | A subscription was never ended                               | `takeUntilDestroyed`                                         |
| Updates stop after the first error         | `catchError` is outside the `switchMap`                      | Move it to the inner observable and return `EMPTY`           |
| An old answer overwrites a new one         | `mergeMap` where `switchMap` was needed                      | `switchMap`, and keep a request id as `latestRequest` does   |
| A retry returns the same failure instantly | The source was not re-created for each try                   | `defer(() => call(...))`                                     |
| A test hangs                               | It waits for a stream that never completes                   | Use `of(...)`/`firstValueFrom`, or fake timers and `take(1)` |

## Terms

| Term       | Meaning                                                                         |
| ---------- | ------------------------------------------------------------------------------- |
| Observable | A recipe for a sequence of values over time                                     |
| Subscribe  | Run the recipe and receive its values                                           |
| Operator   | A function that turns one observable into another                               |
| Subject    | An observable you can push values into                                          |
| Flattening | Turning "an observable of observables" into one: switch, merge, concat, exhaust |
| Teardown   | The cleanup that runs on unsubscribe                                            |
| Cold / hot | Cold starts a new run per subscriber (HTTP); hot shares one source (a Subject)  |

## Further reading

- rxjs.dev: "Overview", "Operators" and the operator decision tree ("Which operator do I use?").
- Angular documentation: "RxJS interop with Angular signals".
- In this repo: `client/src/app/core/state/latest-request.ts` and its spec; `docs/05-frontend.md`.
