# ESLint, Prettier and stylelint

> Three tools that keep front-end code clean without human effort: **ESLint** finds bugs and rule breaks, **Prettier** formats everything one way, **stylelint** checks the stylesheets. They are the TypeScript counterparts of the compiler analyzers and `.editorconfig` on the .NET side.

**Read time:** 15 min · **Version:** ESLint 10.3, typescript-eslint 8.72, angular-eslint 22.6, Prettier 3.6.2, stylelint 16.10 (`client/package.json`) · **Last verified:** 2026-11-03

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) ·
[How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) ·
[What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

| Tool          | Job                                                         | Checks                                                      |
| ------------- | ----------------------------------------------------------- | ----------------------------------------------------------- |
| **ESLint**    | A **linter**: finds likely bugs and rule violations in code | TypeScript and Angular templates (including accessibility)  |
| **Prettier**  | A **formatter**: rewrites code to one consistent layout     | Everything: TypeScript, HTML, SCSS, JSON, Markdown, YAML... |
| **stylelint** | A linter for CSS and SCSS                                   | Stylesheets                                                 |

A linter has _opinions about meaning_ ("this variable is unused", "this `if` has no braces"). A formatter has _opinions about layout only_ ("break the line here"). They are separate on purpose.

## Why we use it

Formatting arguments waste review time, and unused variables, missing braces and inaccessible markup are easy to miss. A machine applies the same rule every time. Prettier's strength is that it is **opinionated with few options**, so there is nothing to debate. ESLint's strength is a rule for nearly anything and a plug-in
for Angular and accessibility. Together they make the diff show only meaningful changes, which is also why we run Prettier over **every file in the repository**.

## The big idea

> **Prettier decides layout; ESLint decides correctness. Neither is a review topic.**

## How it works inside

**ESLint.**

1. A **parser** (typescript-eslint's) turns each file into an **abstract syntax tree (AST)**, the same idea as the C# syntax tree in the [Roslyn primer](roslyn-conventions-tests.md).
2. Each **rule** is a small program that registers interest in node types ("every function declaration") and is called as ESLint **walks** the tree. When a rule sees something wrong it **reports** a message at that location.
3. The **flat config** (`eslint.config.js`) lists which files get which rules and at what severity (`error`, `warn`, `off`). Presets (`recommended`, `stylistic`, Angular's) switch on groups of rules.
4. **Angular templates** (`.html` and inline templates) are parsed by angular-eslint's template parser, so the same machinery checks them, including **accessibility rules** (for example, an element with a click handler needs a keyboard handler and a role).
5. **Type-aware** rules can also ask the TypeScript compiler for types, at a cost in speed.

**Prettier.**

1. It **parses** the file into an AST and **discards** the original layout entirely.
2. It **prints** the tree again from scratch using its own algorithm: fit as much as possible on a line up to `printWidth`, break where needed, always the same way. The output depends only on the tree and a few options, not on how the code was typed.
3. Plug-ins and parsers teach it other languages (we use ones for XML, shell, INI/`.editorconfig`, and Angular templates).
4. **Whitespace sensitivity.** In HTML, a space can change what is shown ("page ." versus "page."). We set `htmlWhitespaceSensitivity: strict` so Prettier never adds or removes meaningful whitespace, and mark the rare element where it would still change rendering with `<!-- prettier-ignore -->`.

**Making them agree.** ESLint has formatting rules that could argue with Prettier. `eslint-config-prettier` **turns those off**, and our config lists it _after_ the presets for that reason.

**stylelint** parses SCSS and applies rules the same way ESLint does.

```
  source --Prettier--> one layout (rewrites the file)          [format:check fails if any file would change]
  source --ESLint parser--> AST --rules visit nodes--> errors   [ng lint fails on any error]
  scss   --stylelint--> errors                                  [lint:styles]
```

## How it fits with everything else

- They are gates **G-05** (format) and **G-06** (lint), tiers 1 and 2 ([Quality gates](../03-quality-gates.md)).
- `npm run typecheck` (`tsc`) is separate and covers what linting does not: types in specs and e2e ([Jest primer](jest.md)).
- The .NET side has **`.editorconfig` plus analyzers** and `dotnet format` ([MSBuild primer](msbuild-and-build-rules.md)), and **Ruff** for the Python tools; one standard across languages.
- The **three-letter-name rule** exists in both worlds: ESLint `id-length` here, a Roslyn conventions test in C# ([primer](roslyn-conventions-tests.md)).
- A root Prettier configuration formats documentation, YAML, JSON, project files and shell scripts too, so a whole repository has one look.

## An example from our code

`client/eslint.config.js` (excerpt):

```js
rules: {
  // A name says what the thing is: a variable, a parameter (an arrow function's included) or a function is at least three
  // letters, payment => payment.amount, never p => p.amount. `_` marks one that is unused; `id`, `to` and `of` are words ...
  'id-length': ['error', { min: 3, exceptions: ['id', 'to', 'of'], exceptionPatterns: ['^_'], properties: 'never' }],
},
...
// Last, so it turns off any rule that would argue with Prettier about layout: Prettier formats, ESLint finds bugs.
prettier,
// After Prettier's config, which would switch it off: braces on every if, else, for and while, one statement or many.
{ files: ['**/*.ts'], rules: { curly: ['error', 'all'] } },
```

- `id-length` (minimum three letters, with a few exceptions) is the same naming rule as the C# conventions test; `properties: 'never'` skips object property names because many are stored or sent data (a draft's `v`), and renaming them would change a format.
- The order matters: **Prettier's config comes after the presets** (so it can switch off layout rules), and `curly: all` comes **after** Prettier's config, because that config would otherwise disable it. A second line under a braceless `if` is a classic bug; braces keep every diff to the lines that changed.
- Angular templates get `templateRecommended` and **`templateAccessibility`**, which is how a `<div (click)>` without a keyboard handler failed CI once.

`client/.prettierrc.json`:

```json
{
  "printWidth": 160,
  "singleQuote": true,
  "plugins": ["@prettier/plugin-xml", "prettier-plugin-sh", "prettier-plugin-ini"],
  "overrides": [{ "files": ["*.html"], "options": { "parser": "angular", "htmlWhitespaceSensitivity": "strict" } }]
}
```

## How to use it

```
cd client
npm run format           # Prettier --write: rewrite files (run before committing)
npm run format:check     # CI: fails if any file would change
npm run lint             # ng lint: ESLint on TypeScript and templates
npm run lint:styles      # stylelint on SCSS
npm run typecheck        # tsc on specs and e2e
```

In the editor, install the ESLint and Prettier extensions and turn on "format on save", so most of this never reaches the command line. To disable a rule for one line: `// eslint-disable-next-line rule-name -- reason`; **the reason is mandatory** (a check that rejects a bare disable is still to be added).

## The rules we follow

| Rule                                                                      | Enforced by                     |
| ------------------------------------------------------------------------- | ------------------------------- |
| Every file is Prettier-formatted (code, docs, YAML, JSON, props, scripts) | `format:check` in CI (G-05)     |
| Braces on every control statement                                         | ESLint `curly` (G-06)           |
| Names of three letters or more                                            | ESLint `id-length`              |
| Angular templates pass the accessibility rules                            | `templateAccessibility` (G-06)  |
| Formatting-only commits go in `.git-blame-ignore-revs`                    | Practice                        |
| An ESLint disable carries a reason                                        | A conventions rule (to add)     |
| No `any`, no `console.log` in application code                            | ESLint rule or ratchet (to add) |

## What goes wrong

| Symptom                                                    | Cause                                         | Fix                                                           |
| ---------------------------------------------------------- | --------------------------------------------- | ------------------------------------------------------------- |
| `format:check` fails in CI but the file looks fine         | Not formatted locally; line endings           | `npm run format`; check `.gitattributes`                      |
| Prettier changes how a template renders ("page ." appears) | Whitespace-sensitive inline content           | `<!-- prettier-ignore -->` on that element; compare rendering |
| ESLint and Prettier fight                                  | A formatting rule left on                     | `eslint-config-prettier` last; remove the rule                |
| A rule silently does nothing                               | Placed before another config that disables it | Order the config; test with a deliberate violation            |
| Lint is slow                                               | Type-aware rules everywhere                   | Limit them to the files that need them                        |
| A huge formatting diff buries a real change                | Formatting mixed with logic                   | Format in its own commit, add to `.git-blame-ignore-revs`     |

## Terms

| Term             | Meaning                                                           |
| ---------------- | ----------------------------------------------------------------- |
| **Linter**       | A tool that flags suspicious or rule-breaking code                |
| **Formatter**    | A tool that rewrites code layout                                  |
| **AST**          | Abstract syntax tree: the structure of code as nested nodes       |
| **Flat config**  | ESLint's current configuration format: an array of config objects |
| **Preset**       | A named bundle of rules                                           |
| **`printWidth`** | The line length Prettier tries to stay within (160 here)          |

## Further reading

- [ESLint](https://eslint.org/docs/latest/) and [typescript-eslint](https://typescript-eslint.io/)
- [angular-eslint](https://github.com/angular-eslint/angular-eslint)
- [Prettier: rationale](https://prettier.io/docs/rationale)
- [stylelint](https://stylelint.io/)
