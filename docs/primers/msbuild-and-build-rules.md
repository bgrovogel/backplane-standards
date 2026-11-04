# MSBuild and build rules

> MSBuild is the program that turns our C# projects into running code. We use its settings files to make the _build itself_ enforce our standards, so a
> rule broken in code fails the build instead of waiting for a reviewer.

**Read time:** 15 min · **Version:** .NET SDK 10.0.100 or newer (`global.json`) · **Last verified:** 2026-11-03 against `mortgage-analytics`

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) ·
[How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) ·
[What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

**MSBuild** is Microsoft's build engine. A `.csproj` file is not a list of source files; it is a small **build script written in XML**. It names the target framework
(`net10.0`), the packages the project needs, and settings that change how the compiler behaves. When you run `dotnet build`, the .NET command-line tool hands the project
to MSBuild, which runs the C# compiler and everything around it (restoring packages, running analyzers, copying files).

Four related things live in the same family and are explained here because they are how our build enforces rules:

- **`Directory.Build.props`**: a file MSBuild automatically reads for every project below it.
- **Central package management** (`Directory.Packages.props`): one place that says which version of each package every project uses.
- **Lock files** (`packages.lock.json`): a recorded list of the exact packages a restore resolved.
- **Analyzers and `.editorconfig`**: rule checkers that run inside the compiler, and the file that sets how strict each rule is.

## Why we use it

Without these, every project in a solution would repeat the same settings, drift apart, and each rule would depend on someone remembering it. Put once in a shared file, a
setting applies to every project, including ones created next year. Making a rule a **compile error** is the strongest enforcement there is: code that breaks it does
not build, so it cannot be merged by accident.

Alternatives we did not choose: a separate linter run only in CI (slower feedback, easy to skip), and per-project settings (drift).

## The big idea

> **The build is a gate.** Settings in shared files turn style, safety and documentation rules into compiler errors for every project.

## How it works inside

1. **Evaluation.** MSBuild reads the `.csproj` and, before it, any `Directory.Build.props` it finds. It looks in the project's folder, then each parent folder, and imports the
   **first one it finds**. It does not import all of them, which is why a nested props file must import its parent on purpose
   (`<Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', ...))" />`, as `tests/Directory.Build.props` does).
2. **Properties and items.** A _property_ is a named setting (`<Nullable>enable</Nullable>`). An _item_ is a list entry (a `PackageReference`, a source file). `Condition`
   attributes switch them on and off, for example only for projects whose name ends in `Tests`.
3. **Restore.** `dotnet restore` reads the package references, asks the package source for each package and its dependencies, picks versions, and writes the result to
   `obj/project.assets.json`.
   - With **central package management**, projects write `<PackageReference Include="NUnit" />` with no version; the version comes from `Directory.Packages.props`.
   - With a **lock file** (`RestorePackagesWithLockFile`), the resolved versions, including indirect ones, are written to `packages.lock.json`. In **locked mode**
     (`dotnet restore --locked-mode`, used in CI), restore fails if the result would differ from the file. So a package cannot change under us unnoticed.
4. **Compile.** MSBuild calls the C# compiler (Roslyn). Roslyn loads each **analyzer** (a library of rules that inspects the code as it compiles) and runs it. Every rule has a
   **severity** (none, suggestion, warning, error). Severities come from `.editorconfig` and from properties such as `TreatWarningsAsErrors`.
5. **Style rules in the command line.** The IDE-style rules (formatting IDE0055, unused usings IDE0005, braces IDE0011) normally run only in the editor. Setting
   `EnforceCodeStyleInBuild` makes them run during `dotnet build` too, and the `.editorconfig` severity decides whether they fail it.
6. **Documentation.** `GenerateDocumentationFile` makes the compiler write XML comments to a file; with it on, a **public member without a summary raises warning CS1591**, which
   `TreatWarningsAsErrors` turns into a failure.

```
  Directory.Build.props  ---->  every project inherits the settings
  Directory.Packages.props --->  versions chosen once
  .editorconfig  ------------->  how strict each analyzer rule is
  global.json  --------------->  which SDK version is used
          |
          v
   dotnet restore (locked-mode) -> dotnet build -> analyzers run -> warnings are errors -> artifact, or a failed build
```

## How it fits with everything else

- The **conventions tests** ([Roslyn primer](roslyn-conventions-tests.md)) cover what analyzers cannot say strongly enough; both feed the same gate family (G-01 to G-08).
- **CI** runs `dotnet restore --locked-mode` then `dotnet build`, so the settings here are exactly what CI enforces ([Quality gates](../03-quality-gates.md)).
- The **standards repository** ships these files as packages so a new app inherits them (strategy, [document 0](../00-strategy.md)).
- **Prettier and ESLint** do the same job for the front end ([primer](eslint-prettier-stylelint.md)).

## An example from our code

`mortgage-analytics/Directory.Build.props` (read 2026-11-03):

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <AnalysisLevel>latest-recommended</AnalysisLevel>
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
</PropertyGroup>
<PropertyGroup Condition="!$(MSBuildProjectName.EndsWith('Tests'))">
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

Line by line:

- `TargetFramework` says which .NET to build for, once, for every project.
- `Nullable` enables the compiler's null checking: a variable that can be null must say so, and using it unchecked is a warning.
- `TreatWarningsAsErrors` makes any warning stop the build. This is why a missing XML summary or an unused using fails.
- `AnalysisLevel` `latest-recommended` turns on the .NET team's current recommended rule set.
- `EnforceCodeStyleInBuild` runs the style rules in the command-line build.
- `RestorePackagesWithLockFile` writes `packages.lock.json` beside each project.
- The `Condition` applies the documentation rule only to production projects (test method names are the documentation).

`tests/Directory.Build.props` then adds the whole test stack (NUnit, NSubstitute, coverlet and their analyzers, global usings) to every project whose name ends in `Tests`,
so no test project repeats it.

## How to use it

```
dotnet restore --locked-mode      # fails if packages.lock.json is out of date
dotnet build -c Release           # the gate: warnings are errors
dotnet format --verify-no-changes # formatting only, if you want it alone
dotnet list package --vulnerable --include-transitive
dotnet list package --outdated
```

To add a package: add a `<PackageVersion>` to `Directory.Packages.props`, a version-less `<PackageReference>` to the project, run `dotnet restore` (this rewrites the lock
file), and commit both. Then write its [primer](../10-documentation-standard.md#primers).

## The rules we follow

| Rule                                                                  | Enforced by                                                 |
| --------------------------------------------------------------------- | ----------------------------------------------------------- |
| One set of settings for every project, in `Directory.Build.props`     | Review; a conventions check for stray settings (to add)     |
| Versions only in `Directory.Packages.props`                           | The build (central management refuses versions in projects) |
| Lock files committed; CI restores in locked mode                      | CI step (G-03)                                              |
| Warnings are errors; style rules run in the build                     | G-01, G-02                                                  |
| Public API has XML documentation                                      | CS1591 (G-04)                                               |
| The SDK is pinned with `global.json` and `rollForward: latestFeature` | The SDK itself                                              |

## What goes wrong

| Symptom                                                            | Cause                                                                       | Fix                                                                 |
| ------------------------------------------------------------------ | --------------------------------------------------------------------------- | ------------------------------------------------------------------- |
| "A compatible .NET SDK was not found"                              | `global.json` wants a newer SDK than installed                              | Install it side by side (`winget install Microsoft.DotNet.SDK.10`)  |
| Restore fails: "The packages.lock.json is not up to date" (NU1004) | A package changed without regenerating the lock file                        | Run `dotnet restore` (no `--locked-mode`), commit the new lock file |
| Build passes in the editor, fails on the command line              | A style rule only ran in the command-line build (`EnforceCodeStyleInBuild`) | Run `dotnet build` before pushing                                   |
| A settings change in a nested folder is ignored                    | MSBuild imports only the first `Directory.Build.props` going up             | Import the parent explicitly                                        |
| A build fails but the dev API still runs and locks the DLLs        | A running process holds `bin/`                                              | Stop the API before building, or use `--artifacts-path`             |

## Terms

| Term                           | Meaning                                                                                |
| ------------------------------ | -------------------------------------------------------------------------------------- |
| **SDK**                        | The .NET toolkit (compiler, build, CLI); not the same as the runtime that runs the app |
| **Analyzer**                   | A library of rules that inspects code while it compiles                                |
| **Severity**                   | How serious a rule's finding is: none, suggestion, warning, error                      |
| **Central package management** | One file that decides every package version for the whole solution                     |
| **Lock file**                  | A record of the exact packages restored, so a build is repeatable                      |
| **`.editorconfig`**            | A plain file of editor and analyzer settings, read by the compiler, IDEs and Prettier  |

## Further reading

- [MSBuild documentation](https://learn.microsoft.com/visualstudio/msbuild/msbuild)
- [Customize your build (Directory.Build.props)](https://learn.microsoft.com/visualstudio/msbuild/customize-your-build)
- [Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management)
- [Code analysis in .NET](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/overview)
