# .github

What GitHub runs for this repository: one workflow, `workflows/ci.yml`, on every push to `main` and every pull request. It builds with warnings as errors and runs every test, then checks that the
documents are formatted and every relative link holds. The repository is public, so its minutes are free; nothing here publishes a package or pushes anything.

**What does not live here:** release or publishing steps (a package is published from the owner's machine after the full local gates), or a schedule. **Who may depend on it:** nothing.
**Read more:** [Repositories and delivery](../docs/09-repositories-and-delivery.md#the-ci-minutes-budget), [Quality gates](../docs/03-quality-gates.md).
