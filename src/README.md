# src

The packages this repository publishes. One folder each, with its own README.

| Package        | What it is                                                                                                                                 |
| -------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| `Platform.Sdk` | The build rules every application starts with, delivered as an MSBuild SDK ([ADR 0012](../docs/adr/0012-build-rules-as-an-msbuild-sdk.md)) |

**What does not live here:** application code, and documents (those are in `docs/`). **Who may depend on it:** applications, by package version, never by path.
**Read more:** [Roadmap, Phase 1](../docs/11-roadmap.md).
