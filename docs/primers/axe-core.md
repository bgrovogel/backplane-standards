# axe-core

> axe-core is an accessibility rule engine. We run it inside Playwright on every page, in both themes and each language, and the build fails if it finds a violation. It finds roughly a third of real accessibility
> problems; the rest need a person.

**Read time:** 12 min · **Version:** `@axe-core/playwright` 4.13 (`client/package.json`) · **Last verified:** 2026-11-03

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) ·
[How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) ·
[What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

**Accessibility** means people can use the product whatever their abilities and tools: with a screen reader, a keyboard only, magnification, high contrast, voice control. The reference standard is **WCAG** (the Web Content Accessibility Guidelines), which we meet at level **AA**.

**axe-core** (from Deque) is a JavaScript library that inspects a web page's DOM and applies a library of rules derived from WCAG. `@axe-core/playwright` runs it inside a Playwright page and returns the results to the test.

## Why we use it

Accessibility is a legal requirement in many markets and the right thing to do, and regressions are easy: a colour tweak breaks contrast, a new button has no name, a heading level is skipped. Humans do not notice these in review. An automated scan on every page, every theme and every language catches the detectable class of
problems on **every** change at almost no cost.

It is not enough alone. Automated tools cannot judge whether alt text is meaningful, whether focus order makes sense, or whether a screen reader's reading is clear. So we add keyboard tests, reader-loss tests, and scheduled manual passes ([Testing strategy](../04-testing-strategy.md#accessibility-axe-and-manual)).

## The big idea

> **Compute what an assistive technology would see, and check it against the rules, automatically and on every page.**

## How it works inside

1. **Injection.** `new AxeBuilder({ page })` makes Playwright inject the axe-core script into the page (`page.evaluate`). It runs in the page's own JavaScript context, so it sees the real, rendered DOM and the browser's computed styles.
2. **The accessibility tree.** Browsers build, beside the DOM, an **accessibility tree**: for each element a **role** (button, heading, link), an **accessible name** (computed from text content, `aria-label`, `<label>`, `alt`), and **states** (expanded, disabled). Screen readers read this tree. axe computes the same things.
3. **Rules.** Each axe **rule** has a **selector** (which elements it applies to) and **checks** (`any`, `all`, `none`). For example `button-name`: applies to `button`; passes if the computed name is non-empty. `color-contrast`: applies to text elements; computes foreground and background colours (resolving
   overlays and opacity) and the contrast ratio, and compares with 4.5:1 (3:1 for large text).
4. **Tags.** Rules carry tags: `wcag2a`, `wcag2aa`, `wcag21aa`, `wcag22aa`, `best-practice`. `withTags([...])` selects which to run. We run WCAG 2.0 A and AA, 2.1 A and AA, and 2.2 AA.
5. **Results.** The returned object lists:

| Result           | Meaning                                                                | What we do           |
| ---------------- | ---------------------------------------------------------------------- | -------------------- |
| **violations**   | A rule failed on specific elements, with an impact (minor to critical) | The test fails       |
| **passes**       | A rule applied and passed                                              | Nothing              |
| **incomplete**   | axe could not decide (for example a background image under text)       | Reviewed by a person |
| **inapplicable** | No element matched the rule                                            | Nothing              |

6. **Limits.** axe judges **what can be computed**. It cannot tell whether an image's alt text describes it, whether a link's purpose is clear, whether the tab order is logical, or whether a custom widget behaves like the role it claims. It analyses one DOM state at a time, so we scan after each interesting state (a dialog open, a section expanded).

```
   Playwright page --> axe script injected --> reads DOM + computed styles + accessibility tree
        --> applies rules tagged WCAG 2.x A/AA --> { violations, passes, incomplete, inapplicable }
        --> our helper turns violations into lines: "color-contrast (serious): .hint, .label"  --> test fails if any
```

## How it fits with everything else

- It runs inside **Playwright** ([primer](playwright.md)), on the production build, so it sees real styles and the real security headers.
- It is gate **G-26**; our own e2e checks for keyboard, reader-loss, touch targets and forced colours cover what axe cannot ([testing strategy](../04-testing-strategy.md#the-catalog)).
- **ESLint's Angular template accessibility rules** catch some problems earlier, at the source ([primer](eslint-prettier-stylelint.md)).
- Hyvva keeps a ratchet that every e2e page list includes an axe scan (`E2eAxeCoverageRatchetTests`), which the platform adopts.
- It supports the "UI work meets accessibility" item in the [definition of done](../03-quality-gates.md#definition-of-done).

## An example from our code

`client/e2e/accessibility.spec.ts`:

```ts
const WCAG = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

/** The page's violations, one line each: "color-contrast (serious): .hint, .label". */
async function violations(page: Page): Promise<string[]> {
  const results = await new AxeBuilder({ page }).withTags(WCAG).analyze();
  return results.violations.map(
    (result) =>
      `${result.id} (${result.impact}): ${result.nodes
        .map((nodeResult) => nodeResult.target.join(' '))
        .slice(0, 5)
        .join(', ')}`,
  );
}
```

- `WCAG` is the list of rule tags to run: levels A and AA of WCAG 2.0, 2.1 and 2.2.
- `analyze()` returns the full result object; the helper keeps only the **violations** and turns each into a one-line summary (rule id, impact, up to five offending selectors) so a failure points straight at the element.
- The spec file's comment states the honest limit: "Automated checks find about a third of real problems (contrast, names, roles, structure); keyboard use and screen-reader wording still need a person."
- The spec scans **every page and result view, in light and dark mode, on desktop and a phone**; the language spec scans the pages in Spanish. A helper `settled(page)` waits until the page has finished loading (`aria-busy="false"`) before scanning, so the scan does not see a half-built page, and `openSections(page)` expands every collapsible section so hidden fields are scanned too.

## How to use it

In a Playwright test:

```ts
import AxeBuilder from '@axe-core/playwright';

const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag22aa']).analyze();
expect(results.violations, 'the page has no detectable accessibility violations').toEqual([]);
```

Scan **after** the state you care about is on screen: the page loaded, the dialog open, the error shown. To exclude something deliberately (a third-party widget), use `.exclude('#selector')` and write why; do not disable a rule broadly.

When a violation appears: read the rule's help URL (in the result), fix the **cause** in the component (a real label, a better colour token), and rerun. Never silence it to get green.

## The rules we follow

| Rule                                                                                      | Enforced by                                                                |
| ----------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| Zero violations on every page, in every theme and language                                | `accessibility.spec.ts`, `language.spec.ts` (G-26)                         |
| Scan each interesting state: loaded, expanded, dialog open, error                         | Review; the spec's helpers                                                 |
| Never exclude a rule or an element without a written reason                               | Review                                                                     |
| Automated is the floor: keyboard, reader-loss and manual screen-reader passes also happen | [Testing strategy](../04-testing-strategy.md#accessibility-axe-and-manual) |
| Colours come from design tokens that already meet contrast                                | The UI kit; axe                                                            |
| New pages are added to the axe page list                                                  | Coverage ratchet (to add)                                                  |

## What goes wrong

| Symptom                                              | Cause                                         | Fix                                                 |
| ---------------------------------------------------- | --------------------------------------------- | --------------------------------------------------- |
| A violation appears only sometimes                   | The scan ran before the page settled          | Wait for the page's ready signal first              |
| `color-contrast` is reported as incomplete           | A gradient or image behind text               | Check manually; avoid text over images              |
| Everything passes but a screen reader user is lost   | Automated tools cannot judge meaning or order | The manual NVDA or VoiceOver pass                   |
| A violation inside an embedded widget you do not own | Third-party markup                            | Report upstream; exclude with a documented reason   |
| Dark theme fails but light passes                    | A token has low contrast in one theme         | Scan both themes (we do)                            |
| The scan is slow                                     | Scanning huge pages repeatedly                | Scan once per state; narrow with `.include('main')` |

## Terms

| Term                   | Meaning                                                                        |
| ---------------------- | ------------------------------------------------------------------------------ |
| **WCAG**               | Web Content Accessibility Guidelines: the international accessibility standard |
| **Level A / AA**       | Conformance levels; AA is the usual legal and practical target                 |
| **Accessible name**    | The text a screen reader announces for an element                              |
| **Role**               | What an element is for assistive technology: button, heading, link             |
| **Accessibility tree** | The browser's description of the page for assistive technology                 |
| **Contrast ratio**     | How different the text colour is from its background, as a number              |
| **Screen reader**      | Software that reads the interface aloud (NVDA, VoiceOver, JAWS)                |

## Further reading

- [axe-core rules](https://dequeuniversity.com/rules/axe/)
- [`@axe-core/playwright`](https://playwright.dev/docs/accessibility-testing)
- [WCAG 2.2 quick reference](https://www.w3.org/WAI/WCAG22/quickref/)
