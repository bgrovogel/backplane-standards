# 14. Ways of working

> How work is found, done, checked, committed and pushed, by a person or by an agent working with or without the owner. These are the habits `mortgage-analytics` settled
> into, written down so a new application, and a new session, starts with them.

**Read time:** 15 min · **Best for:** starting a session, pushing a change, working while the owner is away · **Builds on:**
[Principles](01-principles.md), [Quality gates](03-quality-gates.md), [Repositories and delivery](09-repositories-and-delivery.md#the-ci-minutes-budget)

**On this page:** [Finding work](#finding-work) · [Doing it](#doing-it) · [Working alone](#working-alone-the-owner-is-away) · [Committing](#committing) · [Pushing and CI](#pushing-and-ci) ·
[Keeping the docs true](#keeping-the-docs-true) · [Changing what people see](#changing-what-people-see) · [The machine](#the-machine-windows-and-limited-memory)

---

## Finding work

The plan is **one table** (`docs/work-board.md` in `mortgage-analytics`): an id, a task, a status, who may do it, what it waits for, what "done" looks like, and the result (the commit).
A small tool (`tools/doc-check/board.py`) answers `next` (the task in progress, else the first ready one), `next --unattended` (only tasks marked safe to do while the owner is away)
and `check` (the board is valid: no lost id, no loop, nothing marked done without a commit). A test runs `check`, so a board that rots fails the build.

| Status    | Meaning                                   |
| --------- | ----------------------------------------- |
| `todo`    | Not started                               |
| `doing`   | In progress; **at most one**              |
| `blocked` | Cannot go on; the result column says why  |
| `done`    | Finished; the result column is the commit |

| Who     | Meaning                                                                                            |
| ------- | -------------------------------------------------------------------------------------------------- |
| `auto`  | Safe while the owner is away: a measurement, a test, a document, a refactor with no visible change |
| `ask`   | Needs a decision first: a choice between designs, a change to a public contract, a new dependency  |
| `owner` | Only the owner can do it (a screen-reader pass, a real phone)                                      |

At the start of a session, and after finishing a task, ask the board. **Work that task and no other.** If the owner gives a different instruction, do that and leave the board alone.
Every repository keeps a `CLAUDE.md` (the rules for an agent working there) that says how to find work and how to do it, and points to the README and docs for what the thing is.

---

## Doing it

- **Test first.** Write the failing test, see it fail for the right reason, make it pass. A convention or a guard is proved the same way: break the thing on purpose and see the check go red.
- **Improve what exists.** Do not add features nobody asked for. When asked for improvements, the answer is a better version of what is there.
- **Read it as a person, not as a test.** A passing test proves a number, not that the page makes sense. For anything a person reads, read it once the way they would and ask what they would not understand.
- **Run the checks in pieces, in the foreground** ([the machine](#the-machine-windows-and-limited-memory)): format, lint, typecheck, convention tests, the tests of what changed, the docs checks; then the slow ones.
- **Every change updates the facts that depend on it:** test counts, the README, the guide that describes the behaviour, a primer or a decision record if a tool or a hard-to-reverse choice was added.
- **A new folder gets a README and a regenerated `MAP.md`.**
- **Money is `decimal`; text shown to a person goes through the translation function** and has its Spanish added, in the same change.

---

## Working alone (the owner is away)

A scheduled or looping session takes only `--unattended` tasks, one at a time, and for each one:

1. Set its status to `doing` in the same commit as the work.
2. Write the failing test first, make it pass, then run the checks (`check fast`, and `check full` when client or API code changed).
3. Commit locally with a message that says **why**. Run the board and docs checks first. A commit cannot name its own hash, so record it afterwards in a second, board-only commit: status `done`, the result the first commit's hash.
4. **Stop and mark the task `blocked` (with the reason) instead of pushing on** if a check fails and the cause is not clear in a few tries; the task needs a decision; it would change what a user sees; or it touches `ask` or `owner` work.
5. Take the next `--unattended` task, until none is ready or three are done in one session. End by listing what was done and what is blocked.

**Never, unattended:** push, open a pull request, add a CI job or workflow, change a gate to make it pass (a threshold, budget, ceiling or lint rule), delete a test, force or amend anything already committed, or stage files you did not change
(other sessions leave edits in the tree; for a one-line change in a file with someone else's edits, stage a blob: `git hash-object -w`, then `git update-index --cacheinfo`).

---

## Committing

- **The message says why.** The subject is the change in plain words; the body is the reason, what was tried, what was seen failing first.
- **A commit ends with the co-author line** the tool supplies; a pull request body ends with its generated-by line.
- **A change to code, tests, configuration or dependencies never carries the skip marker. A documentation-only commit does.** CI minutes are limited, so a docs-only change should not spend a run.
- **The skip marker hazard:** GitHub reads the marker anywhere in the head commit's message, even when the message only _mentions_ it. Two pushes once ran no CI because their messages explained the marker.
  Before a push that needs CI, check the head commit does not contain it.
- **Stage only what you changed.** Another session's uncommitted edits stay in the tree and are never staged with a blanket add.
- **The pull request template** is the definition of done: a test that failed first, the build and convention tests, lint/format/typecheck, the docs checks, a README for a new folder, input validated at the boundary with a test at each limit's edges,
  nothing personal or secret logged, authorization and ownership checked, a side effect proved by asserting the call, anything on screen reachable with loading/empty/error states, translated and accessible, the docs updated, no placeholder content reachable, no gate relaxed.

---

## Pushing and CI

- **With the owner in the session, push finished, passing work and report how CI ended.** Do not leave it as local commits and ask. Batch related commits into one push, because CI minutes are limited.
- **After every push, look** (`gh run list`) and report the result. Never say CI "should run": a run that was skipped by mistake, or a red one nobody reported, is how a broken main goes unnoticed.
- **When CI fails on something the local checks passed, find what differs.** Local checks read the working tree; CI reads what was committed. A count in a file another session has edited but not committed is the usual cause: check the committed text (`git show HEAD:path`) and fix a stale line with a staged blob.
- **No new CI jobs, workflows or scheduled runs** (Dependabot, CodeQL, extra mutation projects) to save minutes. Run extra checks locally and report them.
- **Publishing a package is done from the owner's machine** by a script that runs the full local gates, so it costs no CI minutes ([document 9](09-repositories-and-delivery.md#the-package-feed)).

---

## Keeping the docs true

- A number in the prose (a test count, a total) is checked against the real count by a script, and the script runs in CI. A total written as "over 4,600" must be the real total rounded **down** to the hundred.
- Documentation is portable Markdown: tables, headings, quotes, fenced code, box-drawn text diagrams, `<details>`. No Mermaid, GitHub-only alerts or math blocks, because people read it in an editor preview.
- A `file:line` citation is checked by a script; when code moves, the citation and its list are updated in the same change.
- A guide that describes behaviour changes in the same pull request as the behaviour.

---

## Changing what people see

- **Agree the picture before the page.** A change to what a user sees starts with a mockup the owner approves; the app does not change until then. Show the mockups, not a description of them.
- **Measure before adding to a page** (fields and page height at a desktop and a phone width) and again after. See [UI and UX standards](15-ui-and-ux-standards.md).
- **Take your time with a number a person reads.** A reworded sentence, a label, a unit: read the whole page, not just the new line.

---

## The machine (Windows and limited memory)

These are the traps this machine laid, so a new session does not step in them again.

| Trap                                                      | What happens                                                                   | What to do                                                                                                                                                       |
| --------------------------------------------------------- | ------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| The dev API is running                                    | It locks the build output, and a build fails on a locked DLL                   | Stop it, or build to another place (`--artifacts-path <elsewhere>`); do not make a worktree and do not kill the owner's process                                  |
| A scratch worktree with a `node_modules` junction         | Removing the worktree followed the junction and deleted the real packages      | Remove the junction alone first (`cmd /c rmdir`), or run `npm ci` in the worktree instead                                                                        |
| A shell heredoc with apostrophes, `$(...)` or backslashes | It fails to parse, or `\n` arrives as a newline                                | Create the file with the editor tool and run it as a script; use forward slashes in paths                                                                        |
| The machine is short of memory                            | The tool kills long background runs                                            | Run checks in pieces in the foreground (Jest with 2 workers, Playwright in three spec groups, each under 10 minutes). **Do not rerun a killed run on your own.** |
| Many browser windows are open                             | Playwright is slow and a test times out                                        | Rerun with fewer workers before blaming the code; never kill the browser                                                                                         |
| A mutation run needs a lot of memory                      | It is stopped when memory drops, and a build at the same time makes it worse   | Run it alone, with nothing else building; do not start it without the owner's go-ahead                                                                           |
| Tests with a frozen clock                                 | Anything that depends on time passing (expiry, a security stamp) never happens | Advance the clock in a host of its own; keep real and fake time apart on purpose                                                                                 |
| A client-only change                                      | The convention tests still parse the end-to-end specs                          | Run the convention tests before every push, even for client-only changes                                                                                         |
| The local checks pass but CI's docs job fails             | Local reads the working tree; CI reads the commit                              | Check the committed text of any file another session has edited                                                                                                  |

---

## What fails when these are broken

| Habit                                            | What catches it                                         |
| ------------------------------------------------ | ------------------------------------------------------- |
| The board is valid                               | `board.py check`, run by a test                         |
| Counts and citations in the docs are true        | `doc_counts.py` and the citation check, in CI           |
| A new folder has a README and the map is current | A convention test and `repo_map.py --check`             |
| No unattended push                               | Review rule (the agent's `CLAUDE.md`); the owner pushes |
| The skip marker is not in a code commit's head   | Review rule, and the check before a push                |
| Work follows the board                           | Review rule                                             |
