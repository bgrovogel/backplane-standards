# 0004. Few repositories, monorepos for kits, a local-then-private package feed

**Status:** Proposed; **the owner decided on 2026-11-04 that `platform-standards` is public for now** (the kit repositories and packages are still open) · **Date:** 2026-11-03 · **Decided by:** to be accepted by the owner

## Context

The owner expects "each microservice in its own repository". GitHub Free includes about 2,000 Linux Actions minutes a month for private repositories (a $12 allowance; Windows costs about 1.7 times, macOS about 10 times) and free minutes for public repositories using standard runners
([billing page](https://docs.github.com/en/billing/managing-billing-for-your-products/managing-billing-for-github-actions/about-billing-for-github-actions)). GitHub Packages on Free gives private packages 500 MB of storage and 1 GB of transfer a month, free for public packages, and transfer
inside Actions with `GITHUB_TOKEN` is not counted ([billing page](https://docs.github.com/en/billing/managing-billing-for-your-products/managing-billing-for-github-packages/about-billing-for-github-packages)).
The owner has said CI time is limited and `mortgage-analytics` became private on 2026-10-31.

## Decision

We will keep **few repositories**: `platform-standards`, `app-template`, `platform-kit`, `platform-ui`, `platform-capabilities`, plus one per application. Each kit repository is a **monorepo** of many packages, each versioned and published on its own from a git tag. A repository is created only when something
needs to live in it. Packages are published **from the owner's machine** by a release script that runs the full local gates (so publishing costs no CI minutes), first to a **local folder feed**, then to **private GitHub Packages**. Repositories stay **private** until the owner decides to open them.

## Alternatives considered

| Option                                   | Good                                               | Costs                                                                                | Why not                                                 |
| ---------------------------------------- | -------------------------------------------------- | ------------------------------------------------------------------------------------ | ------------------------------------------------------- |
| One repository per capability or library | Strong separation; independent history             | A pipeline, release routine and README each; many version bumps; CI minutes multiply | Unaffordable in time and minutes for one person         |
| One giant repository for everything      | One history, atomic changes                        | Every app's code in one place; CI runs more; hard to open source part                | Applications should deploy and evolve independently     |
| Public repositories and packages         | Free CI and packages; no authentication to restore | The code and patterns are public                                                     | **The owner's decision**; reconsider when ready to open |
| AWS CodeArtifact as the feed             | Next to the rest of AWS                            | One more paid service                                                                | Not needed yet                                          |

## Consequences

- **Easier:** one pipeline and one history per kit; a change to a library and its users together; a small number of things to keep current.
- **Harder:** a monorepo needs path-filtered CI and per-package tags (MinVer with tag prefixes) to avoid releasing everything at once.
- **To verify:** that a reusable workflow in a private repository can be called from another private repository of the same owner ([open verifications](../11-roadmap.md#open-verifications)).
- **Revisit when:** a package needs an independent owner or an external user, or CI usage is regularly near the allowance.
