# 0012. The build rules reach an application as an MSBuild SDK package

**Status:** Accepted for building and trying locally; the feed an application restores it from is still open · **Date:** 2026-11-04 · **Decided by:** the owner approved starting Phase 1; this follows from a spike

## Context

Roadmap task 1.2 lifts `mortgage-analytics`'s `Directory.Build.props`, `.editorconfig` severities and `global.json` into something every application gets from the platform
instead of copying. The obvious shape is a NuGet package whose `build/*.props` file an application references like any package. **It cannot set the target framework.**
NuGet restores before it evaluates a package's props, so a project whose `TargetFramework` comes only from a package's props has none during restore. This was tried on
2026-11-04 with a package that set `TargetFramework`, `Nullable` and `TreatWarningsAsErrors`: the build stopped at `NETSDK1013: The TargetFramework value '' was not recognized`.
The same settings delivered as an **MSBuild SDK package**, named in `global.json` (`"msbuild-sdks": { "Spike.Sdk": "0.0.1" }`) and added to the project with `<Sdk Name="Spike.Sdk" />`,
applied before restore: the project got `net10.0`, and a nullable dereference in `Program.cs` failed the build as an error (CS8602), with the package resolved from a local folder feed.
Building `Platform.Sdk` for real found one more thing: the SDK must be listed **before** the normal one. `Microsoft.NET.Sdk` sets `TreatWarningsAsErrors` to false unless it is already set, so with the order
reversed every analyzer finding was a warning and the build passed.

## Decision

We will deliver the build rules as an **MSBuild SDK package** (`Platform.Sdk`), referenced from `global.json` and listed in each project **before** its normal SDK
(`<Project>`, `<Sdk Name="Platform.Sdk" />`, `<Sdk Name="Microsoft.NET.Sdk" />`, or `Microsoft.NET.Sdk.Web`), so one package serves libraries, web hosts and test projects. The rules that must hold before restore (target framework, nullable,
warnings as errors, analysis level, lock files, central package management) live in its `Sdk.props`; analyzer severities travel as a `.globalconfig` the SDK adds. What an
editor needs to read at once (`.editorconfig`, for formatting) is copied by the application template, because editors do not read a package.

## Alternatives considered

| Option                                            | Good                                                    | Costs                                                                                               | Why not                                  |
| ------------------------------------------------- | ------------------------------------------------------- | --------------------------------------------------------------------------------------------------- | ---------------------------------------- |
| A NuGet package with `build/*.props`              | The familiar way to ship analyzers                      | Cannot set `TargetFramework` or anything restore needs (shown above)                                | Does not do the job                      |
| Copy the files from the template                  | No feed; works today                                    | Every application drifts; a rule change is a pull request into each application                     | This is what the platform exists to stop |
| A shared `Directory.Build.props` by git submodule | One source, no package feed                             | Submodules are hard to keep current; the file is found by path, so nested solutions need imports    | Fragile on Windows and for newcomers     |
| An MSBuild SDK package                            | Applied before restore; versioned; one line per project | Needs a feed an application's restore (and CI) can reach; one more thing to learn (primer to write) | **Chosen**                               |

## Consequences

- **Easier:** a rule changes once and reaches every application by a version bump; the template's projects are one `<Sdk>` line away from every gate.
- **Harder:** an application can only use it where its restore reaches the feed. A local folder works on this machine; **GitHub Actions cannot see it**, so `mortgage-analytics` cannot
  adopt the package, and its own copies stay, until a feed exists that CI can read (private GitHub Packages, [ADR 0004](0004-repositories.md)). Creating that repository and feed is the owner's decision.
- **To do:** write the primer for MSBuild SDKs ([documentation standard](../10-documentation-standard.md)); prove each rule on a sample project that breaks it, as the conventions tests do;
  prove the package through the `app-template` (task 1.10), not through `mortgage-analytics`, until the feed exists.
- **Revisit when:** the feed is chosen (whether `global.json` can name a version range), or a rule appears that an SDK cannot carry.

## References

- [Roadmap, Phase 1](../11-roadmap.md#phase-1-standards-and-the-template), task 1.2
- [MSBuild and build rules, primer](../primers/msbuild-and-build-rules.md)
- [Use MSBuild project SDKs](https://learn.microsoft.com/visualstudio/msbuild/how-to-use-project-sdk) · [Customize your build](https://learn.microsoft.com/visualstudio/msbuild/customize-your-build)
