# 15. UI and UX standards

> What a page must be for the person reading it, and how we keep it that way as features are added. The rules are the ones `mortgage-analytics` learned when a page doubled in length
> after three small additions.

**Read time:** 10 min · **Best for:** adding to a page, designing a screen, reviewing a UI change · **Builds on:**
[Principles](01-principles.md), [Quality gates](03-quality-gates.md) (G-26 to G-28), [Ways of working](14-ways-of-working.md#changing-what-people-see)

**On this page:** [The reader first](#the-reader-first) · [Not flooding a page](#not-flooding-a-page) · [Adding an option](#adding-an-option-to-a-page) · [Popups and focus](#popups-dialogs-and-focus) ·
[Words](#words) · [Accessibility](#accessibility) · [A visual system](#a-visual-system) · [Honesty](#honesty-in-what-is-shown)

---

## The reader first

- **One question per page, and the answer first.** A page opens with the answer to the question it is for (a headline figure, a verdict), then one row of views of it. Depth (a full schedule, a projection) sits one step down.
- **The audience is the person with the decision,** not the engineer. How a number was worked out is one click away, never in the way.
- **Structure follows questions,** not the data model: `mortgage-analytics` has four pages by question (buy a home, refinance, pay off or invest, saved), and what used to be a separate page became a view.
- **The page is read on a phone.** The form may sit behind a button there, so a message never says where the form is ("on the left", "above"); a test guards it.

---

## Not flooding a page

It is fine for an application to hold a lot. It is not fine for it all to sit in one page or one section. A page is split so each part can be read, changed and maintained on its own.

1. **Before adding anything, ask what the reader needs first and what can sit one step away:** a dialog, a "go deeper" view, its own page.
2. **Measure before and after.** Count the visible fields and the page height at a desktop width (1280) and a phone width (390), with a script in the browser test tool. Three small add-ons once took a page from 10 fields to 22 and from 1,478 px to 2,883 px.
3. **Prefer small focused components and stylesheets** over growing one.
4. **Disclose by consequence:** what changes the answer is shown; what only explains it is folded; a fold says in one line what is inside it.
5. **Undo, not confirm,** where the action can be undone.

---

## Adding an option to a page

The pattern agreed after the page that doubled, and the one to follow for any new option on any page:

- Extras go in **one closed form section** (named for what it does, such as "Compare other ways"), with a **one-line summary of what is switched on** in its heading.
- Their results go in **one results card** ("Your options"): one line per way, a question and its answer, opening to its table, open on its own when it is the only one.
- A new option is therefore one more line in the section and one more line in the card, not a new block.

---

## Popups (dialogs) and focus

A popup is right for a short task or a detail that would otherwise crowd the page. Every one:

- takes focus in when it opens, and returns it to what opened it when it closes;
- closes with Escape;
- is labelled for a screen reader, and traps keyboard focus while it is open;
- can be reached and used with the keyboard alone, and by a person using forced colours or 2x text.

The accessibility tests (G-26, G-27) open every dialog; a dialog that fails them does not ship.

---

## Words

- **Every word a person reads goes through the translation function** and has its translation added in the same change. The English text is its own key. A test lists what is missing.
- **What the server refuses with is translated by a code,** not by its English text ([document 5](05-validation-and-errors.md)).
- **Units and money:** money is shown with its currency, rates with the percent sign, durations with their unit; a figure is never shown without what it is.
- **No sentence that depends on layout** ("the box on the right").
- **Plain words first, the jargon one click away** (a glossary link in read-only text; a field label keeps its abbreviation so a click still selects the field).

---

## Accessibility

Accessibility is a gate, not a review. Every route has an automated scan (axe) in every theme and language, and a guard test fails when a route is added without one. The reader-loss checks run too:
nothing is lost at 2x text size, with WCAG text spacing, at 320 px width, in forced colours, or with the keyboard alone. State is never only a colour: a status has a shape or text as well.
A screen-reader pass and a real-phone pass are things only a person can do; they are on the board as the owner's, and what was found is recorded.

---

## A visual system

A product has one visual system: colour roles, type, spacing, themes and their contrast, written as tokens and used through them. `mortgage-analytics` has one ("Precision Instrument": a dark-first look,
a serif for money, a sans for the interface, a monospaced face for ledgers, one action colour, never pure white text on a dark ground). Its numbers belong to that product; the **method** is the standard:

1. Tokens first (colour, type, spacing, elevation, motion), in both themes, with their contrast measured.
2. Components built from tokens only; no hard-coded colour in a component.
3. A gallery page that shows every component in every state, with visual tests.
4. A new product copies the method and chooses its own tokens; the design-system package ([roadmap, Phase 3](11-roadmap.md)) is extracted from a working app, not designed first.

---

## Honesty in what is shown

- **No invented data.** A placeholder in a mockup is filled from real values; a figure that cannot be known is not shown.
- **Say what a number is built from** next to it when it could be misread: whose money, which year's rules, whether a saving is counted.
- **No placeholder or mock content is reachable by a real user.**
- **A thing left out on purpose is written down with the reason** (for example, a figure that would be wrong under the rules the app uses), so it is not rebuilt wrongly later.

---

## What fails when these are broken

| Rule                                     | What catches it                                                               |
| ---------------------------------------- | ----------------------------------------------------------------------------- |
| Every route is scanned for accessibility | A guard test that compares the routes with the scan specs                     |
| Every word is translated                 | A test that lists the messages with no translation                            |
| Nothing says where the form is           | A translations test                                                           |
| A page does not grow unnoticed           | Layout-shift budget (G-28); the before-and-after measurement is a review rule |
| A dialog is operable by keyboard         | The accessibility and reader-loss tests                                       |
| The picture is agreed before the page    | Review rule                                                                   |
