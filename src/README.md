# src

The packages this repository publishes. One folder each, with its own README.

| Package                | What it is                                                                                                                                 |
| ---------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| `Platform.Sdk`         | The build rules every application starts with, delivered as an MSBuild SDK ([ADR 0012](../docs/adr/0012-build-rules-as-an-msbuild-sdk.md)) |
| `Platform.Conventions` | The conventions tests as a package: Roslyn rules and the fixtures an application inherits in one line each                                 |
| `frontend-presets`     | The front-end standards as shared configuration: ESLint with the layer rule, Prettier, stylelint and strict tsconfigs (an npm package)     |

**What does not live here:** application code, and documents (those are in `docs/`). **Who may depend on it:** applications, by package version, never by path.
**Read more:** [Roadmap, Phase 1](../docs/11-roadmap.md).
