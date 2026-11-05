# Platform

How we build every application from now on: one set of standards, one set of reusable capabilities, one starter, so a new
application starts with every quality gate switched on and its common features already built.

This folder is the **plan and the rulebook**, and since 2026-11-04 it is a git repository, public on GitHub as `bgrovogel/platform-standards`: the first
repository of the plan ([document 9](docs/09-repositories-and-delivery.md#repositories); roadmap task 1.1). It keeps the folder name `platform` until the platform
name is chosen. It holds no application code yet.

Status as of 2026-11-04: strategy written, and the standards applied to `mortgage-analytics` first, with no rewrites of other apps ([document 12](docs/12-mortgage-first.md)):
every item of its work list is done except two that need the owner. Phase 1 of the [roadmap](docs/11-roadmap.md) (lifting those standards into packages and an app template)
is next. Nothing is extracted yet.

## Read in this order

| #   | Document                                                          | Read it to learn                                                                                           |
| --- | ----------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| 0   | [Strategy](docs/00-strategy.md)                                   | What we are building, why, what we found in our five existing apps, and the decisions already made         |
| 1   | [Principles](docs/01-principles.md)                               | DRY, KISS, SOLID, Clean Code and the rest, in plain words, with examples and how each one is enforced      |
| 2   | [Architecture](docs/02-architecture.md)                           | Clean Architecture: where every kind of code lives, in the backend, the frontend and across repositories   |
| 3   | [Quality gates](docs/03-quality-gates.md)                         | The strict rules: what must pass before code is merged, released and deployed, with numbers                |
| 4   | [Testing strategy](docs/04-testing-strategy.md)                   | Every kind of test we run, what it proves, and when it runs                                                |
| 5   | [Validation and errors](docs/05-validation-and-errors.md)         | How input is checked, how failures are reported, how we prove a rule is real                               |
| 6   | [Logging and observability](docs/06-logging-observability.md)     | What we log, what we never log, how a problem is found in production                                       |
| 7   | [Security baseline](docs/07-security-baseline.md)                 | The rules every application and capability follows                                                         |
| 8   | [Capability modules](docs/08-capability-modules.md)               | How a reusable feature (login, email, billing) is built so a new app can inject it                         |
| 9   | [Repositories and delivery](docs/09-repositories-and-delivery.md) | Which repositories exist, how packages are published, how we live within the CI-minutes budget             |
| 10  | [Documentation standard](docs/10-documentation-standard.md)       | What must be written for a brand new person, and the template every tool primer follows                    |
| 11  | [Roadmap](docs/11-roadmap.md)                                     | The build order, what "done" means for each step, the first-week checklist, the open decisions             |
| 12  | [Mortgage first](docs/12-mortgage-first.md)                       | The agreed focus: bring `mortgage-analytics` up to the standards first, with measured gaps and a work list |
| 13  | [buybright comparison](docs/13-buybright-comparison.md)           | What buybright has that the mortgage app lacks, and what is worth porting                                  |
| 14  | [Ways of working](docs/14-ways-of-working.md)                     | Finding work, working alone, committing, pushing, keeping docs true, and the traps of this machine         |
| 15  | [UI and UX standards](docs/15-ui-and-ux-standards.md)             | Putting the reader first, not flooding a page, popups, words, accessibility, a visual system, honesty      |

Then the [primers](docs/primers/README.md): one short guide per tool or framework (what it is, why we use it, how it works
inside, how it fits with the others, what goes wrong). If you are new, start with
[How everything fits together](docs/primers/00-how-it-fits-together.md).

Decisions that are hard to reverse are recorded one per file in [docs/adr](docs/adr/README.md).

## Ground rules for this folder

- **Markdown only, portable:** headings, lists, tables and code fences. No Mermaid, no GitHub-only alert boxes, no math
  blocks, because the owner reads these in VS Code's preview.
- **A fact has a source.** A number or a claim about an existing app names the file it came from. A claim we have not
  checked says so, and is listed in [Open verifications](docs/11-roadmap.md#open-verifications).
- **No tool without a primer.** A framework or library is not adopted until its primer exists
  ([Documentation standard](docs/10-documentation-standard.md)).
- **Written to be enforced.** A rule that nothing checks is a wish. Each rule here names what fails when it is broken,
  or says plainly that it is a review rule.
