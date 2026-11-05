# Platform.Sdk.Tests

Proves `Platform.Sdk` by using it. Each test makes a small solution in a temporary folder, packs the SDK from `src/Platform.Sdk` into a folder feed of its own under a version no earlier run
used (and a package cache of its own, so a changed SDK is never read from an old cache), builds the sample, and checks how the build ended.

- `SampleBuild` makes the sample and builds it.
- `BuildRuleTests` holds one test per rule: a clean project builds, and a project that breaks the rule fails with the rule's own diagnostic id (CS8602, CS1591, IDE0005, IDE0011, IDE0161, IDE0055).
  Two more prove an application can override (its `.editorconfig` wins) and that a test project is exempt from CS1591.

**What does not live here:** the SDK itself. **Who may depend on it:** nothing. **Run it:** `dotnet test tests/Platform.Sdk.Tests` (about half a minute: each test runs a real build).
**Read more:** [the primer](../../docs/primers/msbuild-sdks.md).
