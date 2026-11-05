# 10. Documentation standard

> Everything is written for a brand new person: what each thing is, why we use it, how it works inside, and how it all comes together.
> No tool, framework or library is adopted until its primer exists.

**Read time:** 15 min · **Best for:** writing or reviewing documentation · **Builds on:** everything before it ·
**Examples:** the [primers](primers/README.md) are this standard applied

**On this page:** [The audience](#the-audience) · [The kinds of documentation](#the-kinds-of-documentation) · [What must exist](#what-must-exist) ·
[Primers](#primers) · [Writing rules](#writing-rules) · [Showing how it comes together](#showing-how-it-comes-together) ·
[Decisions: ADRs](#decisions-adrs) · [Keeping documentation true](#keeping-documentation-true) · [Onboarding path](#onboarding-path)

---

## The audience

Write for **a capable person who has never seen this code, this tool or this domain**: a new teammate on their first day, the owner returning
after six months, an AI agent starting cold. They can read code; they do not know _why it is this way_. If the document only makes sense to
someone who already understands the thing, it has failed.

The test: **can they find the code that does X, explain in their own words what it does and why, and change it safely, using only the repository?**

---

## The kinds of documentation

Documentation fails when one page tries to be four things. We separate them (the **Diátaxis** model):

| Kind            | The reader wants to...                     | Style                                           | Where we keep it                                  |
| --------------- | ------------------------------------------ | ----------------------------------------------- | ------------------------------------------------- |
| **Tutorial**    | Learn by doing something small, end to end | Step by step, every step works, one safe path   | `docs/tutorials/` ("Add your first capability")   |
| **How-to**      | Get a specific job done                    | A short recipe for someone who knows the basics | `docs/how-to/`, runbooks                          |
| **Reference**   | Look up an exact fact                      | Complete, dry, accurate, often generated        | OpenAPI, options tables, the gates table          |
| **Explanation** | Understand why and how it works            | Narrative with pictures and trade-offs          | The numbered documents, the **primers**, the ADRs |

Most of this folder is explanation and reference. Tutorials and how-tos are written as the first applications are built.

---

## What must exist

| Thing                                         | Must have                                                                                                                                              |
| --------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Every repository**                          | `README.md` (what, why, how to start in five minutes), `MAP.md` (generated map), `CLAUDE.md` (rules for agents), `docs/`                               |
| **Every top-level folder and project**        | A five-line `README.md`: what lives here, what does not, who may depend on it, where to read more                                                      |
| **Every library or capability**               | A page: purpose, how to use it (example that compiles), configuration, how it works, failure modes, version history                                    |
| **Every tool, framework or package we adopt** | A [primer](#primers)                                                                                                                                   |
| **Every hard-to-reverse decision**            | An [ADR](#decisions-adrs)                                                                                                                              |
| **Every deployed capability**                 | Threat model, SLO, one runbook per alarm, a configuration page                                                                                         |
| **Every application**                         | A request walk-through (one request from screen to database and back, with file and line citations), the architecture page, a testing page, a glossary |
| **Every public API**                          | XML documentation on every public member (the compiler enforces it); the OpenAPI is the reference                                                      |
| **Every release**                             | A changelog entry saying what changed and what an application must do to upgrade                                                                       |
| **The whole platform**                        | A [glossary](glossary.md) of every term of art, abbreviation and technology name, in plain words                                                       |

---

## Primers

A **primer** is a short guide to one tool or framework, written so a newcomer can use it with confidence and _understand how it works_, not just
how to call it. It exists because "it just works" is the most expensive sentence in engineering: when the magic fails, the person who never learned
the mechanism is stuck.

**The rule: no tool without a primer.** A pull request that adds a package to `Directory.Packages.props` or `package.json` adds its primer in the
same change, or the documentation check fails (**new**: it compares the dependency lists with the primer index; a deliberate exemption list with a
reason covers pure transitive helpers).

Every primer follows the same template, so a reader knows where to look:

```
# <Tool>

> One sentence: what it is and what we use it for.

Read time · Version we use · Last verified (date)

## What it is
Two or three plain sentences. Define every term the first time it appears.

## Why we use it
The problem it solves, what we would do without it, and the alternatives we considered with why we did not pick them.

## The big idea
The mental model in a paragraph and a picture. If you remember one thing, it is this.

## How it works inside
The mechanism, step by step, with a diagram. This is the section that removes the magic.

## How it fits with everything else
Where it sits among the other tools; what calls it and what it calls. Links to the other primers.

## An example from our code
A real, small example with its file path and line numbers (checked by the documentation check), with the code explained line by line.

## How to use it
The commands and snippets a newcomer needs on day one.

## The rules we follow
Our conventions for it, and which gate enforces each.

## What goes wrong
The common mistakes, the symptom of each, and how to diagnose it.

## Terms
Every abbreviation and jargon word used on the page, in plain words.

## Further reading
The official documentation and one or two good explanations, with links.
```

The primers written so far are listed in the [primer index](primers/README.md), and the first, [How everything fits together](primers/00-how-it-fits-together.md),
is the map that connects them.

---

## Writing rules

1. **Plain words first.** Short sentences. Active voice. If a technical term is needed, **define it the first time it appears** on the page, then use it consistently.
2. **No unexplained abbreviations.** Spell it out once ("row-level security (RLS)"), and add it to the glossary and the page's Terms section.
3. **Say why, not only what.** A rule with no reason gets broken; a rule with a reason gets applied to the cases nobody foresaw.
4. **Show an example from our own code** with its path. Examples beat descriptions. Quote the real file, not an idealized sketch.
5. **Explain the mechanism.** "NSubstitute creates a fake" is not an explanation. "NSubstitute generates, at run time, a class that implements the interface and
   records every call" is.
6. **Lead with the answer.** The first line of a page, and the first line of a section, tells the reader what they will get.
7. **Pictures where they help,** drawn in **portable text** (boxes and arrows in a code block) so they render everywhere, including the owner's VS Code
   preview. No Mermaid, no GitHub-only features, no math blocks.
8. **Tables for facts, prose for reasons.** Do not put an argument in a table cell.
9. **Every claim about the code has a citation** (`path:line`) and a date or version where it can age. A claim we have not verified says so.
10. **State the status honestly:** _exists_, _proposed_, _to verify_, _not built_. Never present a plan as if it were done.
11. **One idea per section, one purpose per page.** If a page needs a table of contents longer than twelve lines, split it.
12. **Write the "what goes wrong" section.** The failures are what the reader will actually meet.

---

## Showing how it comes together

Individual primers explain tools. People also need the **assembly**: how a request, a test or a deploy flows through all of them. Three standing pieces:

1. **[How everything fits together](primers/00-how-it-fits-together.md)** (platform level): one request from the browser to the database and the log, then one change from
   test to production, naming every tool that touches it and linking its primer.
2. **A request walk-through in every application** (as `mortgage-analytics` has in its `docs/03-architecture.md`, "One request, end to end"): numbered steps, each with the
   file and line.
3. **A "what runs when" page** for the pipeline: which gate runs in which tier, with the commands ([document 3](03-quality-gates.md#the-four-tiers)).

When a tool is explained, the page says **what calls it and what it calls**, so the reader can place it on the map.

---

## Decisions: ADRs

An **architecture decision record** is a short file that says _what we decided, why, what else we considered, and what follows_. It is how a
newcomer, or the owner in a year, learns why the code is shaped the way it is.

- **When:** any choice that is hard to reverse or that a reasonable person might question: a language, a framework, a data store, a boundary, a gate being relaxed.
- **Format:** the [template](adr/0000-template.md): status, context, decision, alternatives, consequences.
- **Immutable once accepted:** a decision is changed by a new ADR that supersedes it, so the history stays readable.
- **Where:** `docs/adr/NNNN-title.md`, numbered, indexed in `docs/adr/README.md`.

---

## Keeping documentation true

Documentation that is wrong is worse than none. We treat it as code.

| Mechanism                                                                       | What it catches                                                                     | Status                                                          |
| ------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- | --------------------------------------------------------------- |
| **Docs live in the repository** and change in the same pull request as the code | Drift                                                                               | Exists                                                          |
| **Counts check** (`doc_checks/counts.py`)                                       | A test count, version or total in the prose that no longer matches reality          | Exists in `backplane-standards`; run on `mortgage-analytics`    |
| **Citation check** (`doc_checks/refs.py`)                                       | A `file:line` that no longer points at the cited code                               | Exists in `backplane-standards`; run on `mortgage-analytics`    |
| **Link check** (`doc_checks/refs.py`)                                           | A relative link or anchor that is broken, or one that leaves the repo               | Exists in `backplane-standards`                                 |
| **Primer-per-package check** (`doc_checks/primers.py`)                          | A dependency with no primer, an exemption with no reason, a primer off the template | Exists in `backplane-standards`                                 |
| **README-per-folder check** (`doc_checks/repo_map.py`, and the C# rule)         | A project or top-level folder with no README                                        | Exists in `backplane-standards`                                 |
| **Code snippets are extracted from real code**                                  | An example that no longer compiles                                                  | **New** (snippets marked and compiled in a test project)        |
| **`MAP.md` regenerated and compared** (`doc_checks/repo_map.py`)                | A map that omits a new project                                                      | Exists in `backplane-standards`; run on `mortgage-analytics`    |
| **"Last verified" date and version on each primer**                             | A primer about version 5 describing version 8                                       | **New**; a quarterly review lists primers older than six months |
| **Formatting by Prettier**                                                      | Inconsistent Markdown                                                               | Exists                                                          |
| **Work board check** (`board.py check`)                                         | A plan that lost an id, loops, or marks a task done with no commit                  | Exists in `mortgage-analytics`                                  |
| **Portable Markdown only**                                                      | A diagram or alert that shows as raw text in an editor preview                      | Review rule                                                     |
| **Check the committed text, not the working tree**                              | A stale count that a neighbouring session's uncommitted edit hides locally          | Review rule; see [ways of working](14-ways-of-working.md)       |

**A stale document is a bug.** It gets an issue, an owner and a fix like any other defect.

---

## Onboarding path

What a new person does, and how long it should take. The template repository carries this as `docs/start-here.md`.

| Step | Read or do                                                                                        | Outcome                                                     |
| ---- | ------------------------------------------------------------------------------------------------- | ----------------------------------------------------------- |
| 1    | [Strategy](00-strategy.md) and [How everything fits together](primers/00-how-it-fits-together.md) | They can say what the platform is and draw the request path |
| 2    | [Principles](01-principles.md) and [Architecture](02-architecture.md)                             | They know where code goes and why                           |
| 3    | Run the template: `./check fast`, `./check full`                                                  | The gates run on their machine and pass                     |
| 4    | The primers for NUnit, NSubstitute and Playwright                                                 | They can read any test in the repository                    |
| 5    | Tutorial: add a small use case test-first                                                         | They have felt red, green, refactor with every gate on      |
| 6    | Tutorial: add a capability to the template app and call it                                        | They have used a capability end to end                      |
| 7    | [Quality gates](03-quality-gates.md) and the [security baseline](07-security-baseline.md)         | They know what will stop their change and why               |

If a newcomer cannot finish this path in a few days, **the documentation is the thing to fix**.
