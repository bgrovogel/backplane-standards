# CLAUDE.md

Read by every Claude Code session in this repository. It says how to find work and how to do it. What this repository is and how it is organised is in
[README.md](README.md) and [docs/](docs/11-roadmap.md); the habits behind these rules are in [Ways of working](docs/14-ways-of-working.md).

## Finding work

The plan is the table in [docs/work-board.md](docs/work-board.md): the roadmap's tasks in order, each with who may do it. At the start of a session, and after finishing a task:

```text
python tools/board.py next               # with the owner here
python tools/board.py next --unattended  # owner away: only tasks marked auto
```

Work that task and no other. If the owner gives a different instruction, do that and leave the board alone. The roadmap ([docs/11-roadmap.md](docs/11-roadmap.md)) says why each task exists.

## Working alone (the owner is away)

A scheduled or looping session takes only `--unattended` tasks, at most three in one session, one at a time. For each one:

1. Set its Status to `doing` (the board allows one at a time) in the same commit as the work.
2. Write the failing test first, make it pass, then run the checks (`dotnet test`, `npm run format:check`, `python tools/doc_checks/run.py`, `python -m unittest discover tools`, `python tools/board.py check`).
3. Commit locally with a message that says why. A commit cannot name its own hash, so record it afterwards in a second, board-only commit marked with the skip marker: Status `done`, Result the hash of the first.
4. **Stop and mark the task `blocked` (with the reason in Result) instead of pushing on** if a check fails and the cause is not clear in a few tries, the task needs a decision, or it touches `ask` or `owner` work.
5. End by listing what was done and what is blocked.

## Doing it

1. **No tool or package without a primer** ([documentation standard](docs/10-documentation-standard.md)), and no hard-to-reverse choice without an ADR.
2. **Extract, never invent.** Each piece is lifted from a working app with its tests, then proved by running a new app on it. A rule is proved by a sample that breaks it.
3. **Test first.** Write the failing test, see it fail for the right reason, make it pass.
4. **A fact has a source.** Name the file it came from, or say it is unchecked and list it under open verifications.
5. **Written to be enforced.** A rule nothing checks is a wish: name what fails when it is broken, or say plainly that it is a review rule.
6. **Portable Markdown only** (tables, headings, quotes, fenced code, text diagrams): no Mermaid, no GitHub-only alerts, no math blocks. Format with the shared Prettier configuration before committing.
7. **Update the facts that depend on a change** (counts, links, the README table, the ADR index) in the same commit.

Never, unattended: push, create or change a CI workflow, publish a package, delete or loosen a rule or a gate's threshold, delete a test, force or amend anything already committed, or stage files you did not change.

## Pushing

With the owner in the session, push finished work and report how CI ended. This repository is public, so CI is free; the habit stays: a documentation-only commit may carry the skip marker, and a code commit never does. The marker is read anywhere in the head
commit's message, so do not mention it in a message that needs CI. After a push, `gh run list`.

## Conventions

- This repository is public. Nothing private goes in it: no secret, no personal data, no account number.
- The platform name and the package prefix `Platform` are placeholders until the owner chooses a name.
- On Windows, scripts with backslashes or apostrophes go through the file tool, not a heredoc.
- Commit messages say why, and end with the co-author line the tool supplies.
