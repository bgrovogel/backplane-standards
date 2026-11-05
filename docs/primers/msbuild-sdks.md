# MSBuild SDKs (Platform.Sdk)

> An MSBuild SDK is a package of build rules that a project imports before anything else happens, even before packages are restored. It is how the platform gives every
> application the same strict build, in one line, from one place.

**Read time:** 10 min · **Version:** .NET SDK 10.0.100 or newer (`global.json`) · **Last verified:** 2026-11-04 against `src/Platform.Sdk` and its tests

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) ·
[How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) ·
[What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

Every `.csproj` starts from an **SDK**: `Microsoft.NET.Sdk` is the one that knows how to build a .NET library, `Microsoft.NET.Sdk.Web` adds web hosting. An SDK is two XML files, `Sdk.props`
(read before the project's own body) and `Sdk.targets` (read after), shipped with the .NET tooling. An **MSBuild SDK package** is the same two files delivered as a NuGet package instead,
so anyone can make one. `Platform.Sdk` is ours. It contains no code; it contains our build rules.

## Why we use it

The first idea was a NuGet package whose `build/*.props` file sets the rules. It does not work for the most important rule: **a package's props are read after restore**, and restore needs the
target framework, so a project whose framework comes only from a package fails with `NETSDK1013` (tried on 2026-11-04, [ADR 0012](../adr/0012-build-rules-as-an-msbuild-sdk.md)). An SDK is resolved
before evaluation, so it can set anything. The alternative, copying `Directory.Build.props` into every application, drifts: a rule changes in one place and the other applications never hear.

## The big idea

> **A rule is written once, in a package with a version, and an application picks it up by naming that version.**

A rule change is then a version bump with a test behind it, not a copy-and-paste into four repositories.

## How it works inside

1. The application's `global.json` names the SDK and its version: `"msbuild-sdks": { "Platform.Sdk": "1.2.0" }`.
2. A project lists it **before** its normal SDK:

   ```xml
   <Project>
     <Sdk Name="Platform.Sdk" />
     <Sdk Name="Microsoft.NET.Sdk" />
     ...
   </Project>
   ```

3. MSBuild asks NuGet for that package (from the sources in the application's `nuget.config`), unpacks it, and imports `Sdk/Sdk.props` first. That file sets the defaults:
   `net10.0`, nullable on, warnings as errors, `latest-recommended` analysis, code style enforced in the build, a lock file, an XML documentation file. Every one is written
   `Condition="'$(Name)' == ''"`, so a value set earlier (in an application's `Directory.Build.props`) stays, and the project's own body, which is read after, can always override.
4. `Sdk/Platform.globalconfig` is added as a global analyzer config: the severities (`IDE0055`, `IDE0005`, `IDE0161`, `IDE0011` as warnings, therefore errors) and the style options.
   An application's own `.editorconfig` is read after it and wins.
5. A project whose name ends in `Tests` also gets `CS1591` switched off, because its method names are its documentation.

**The order of the two `<Sdk>` lines matters.** `Microsoft.NET.Sdk` sets `TreatWarningsAsErrors` to `false` unless it is already set. With the SDKs in the other order, every finding was a warning
and the build passed. This was found by a test that failed, which is why each rule has one.

## How it fits with everything else

- It is the delivery mechanism for [MSBuild and build rules](msbuild-and-build-rules.md): the same settings, from one source.
- The conventions tests ([Roslyn conventions tests](roslyn-conventions-tests.md)) cover what a compiler setting cannot; this covers what one can.
- An application still has its own `Directory.Build.props` for anything specific to it, its own `Directory.Packages.props` for versions, and its own `.editorconfig` (editors do not read packages).
- It needs a **feed**: the place its restore finds the package. A local folder works on one machine; CI needs a feed it can reach (private GitHub Packages is the plan, [ADR 0004](../adr/0004-repositories.md)).

## An example from our code

`tests/Platform.Sdk.Tests` makes a small solution in a temporary folder for each test, packs `src/Platform.Sdk` into a folder feed of its own under a version nobody has used, and builds the sample:

```csharp
BuildResult result = BuildLibrary("using System.Text;\n" + GoodCode);

Assert.That(result.Succeeded, Is.False, "the code style is checked by the build");
Assert.That(result.Output, Does.Contain("IDE0005"), "and the build says it was the unused using");
```

A rule is proved by a sample that breaks it. If the rule stops applying, the sample builds and the test fails, here and not in an application.

## How to use it

1. In `global.json`: add `msbuild-sdks` with the version.
2. In `nuget.config`: add the feed.
3. In each `.csproj`: replace `<Project Sdk="Microsoft.NET.Sdk">` with the three lines above, and delete the settings the SDK now gives (`TargetFramework`, `Nullable`, ...).
4. Copy the template's `.editorconfig` for the editor's sake.
5. To change a rule: change `Sdk.props` or `Platform.globalconfig`, add or adjust the sample that proves it, run `dotnet test tests/Platform.Sdk.Tests`, bump the version.

## The rules we follow

| Rule                                                                                | What catches a break                                     |
| ----------------------------------------------------------------------------------- | -------------------------------------------------------- |
| Every rule in `Sdk.props` is written to give way to an earlier value                | Review; a test that overrides one (`.editorconfig` wins) |
| Every rule has a sample project that breaks it                                      | `tests/Platform.Sdk.Tests`                               |
| The SDK holds no code, only settings                                                | Review                                                   |
| A changed SDK is a new version; an application moves to it on purpose               | The version in `global.json`                             |
| What an editor must read (`.editorconfig`) is copied by the template, not delivered | The template (task 1.10)                                 |

## What goes wrong

| Symptom                                                        | Cause                                                      | Fix                                                                      |
| -------------------------------------------------------------- | ---------------------------------------------------------- | ------------------------------------------------------------------------ |
| Warnings do not fail the build                                 | `Platform.Sdk` is listed after `Microsoft.NET.Sdk`         | List it first                                                            |
| `NU1101`/`The SDK 'Platform.Sdk' specified could not be found` | The feed is not in `nuget.config`, or the version is wrong | Check the source and the version in `global.json`                        |
| A change to the SDK is not seen                                | The package of that version is cached                      | Use a new version; tests give each run its own version and its own cache |
| CI cannot restore it                                           | The feed is a folder on one machine                        | A feed CI can reach ([ADR 0004](../adr/0004-repositories.md))            |
| An editor shows no style errors                                | Editors read `.editorconfig`, not a package                | Copy the template's `.editorconfig`                                      |

## Terms

| Term                       | Meaning                                                                                                                 |
| -------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| **SDK**                    | A pair of files, `Sdk.props` and `Sdk.targets`, that tells MSBuild how to build a kind of project                       |
| **MSBuild SDK package**    | An SDK delivered as a NuGet package with the `MSBuildSdk` package type                                                  |
| **`msbuild-sdks`**         | The section of `global.json` that names SDK packages and their versions                                                 |
| **Feed**                   | A place packages are restored from: a folder, or a server                                                               |
| **Global analyzer config** | An analyzer settings file that applies to a whole project, from a package, rather than to a folder like `.editorconfig` |

## Further reading

- [Use MSBuild project SDKs](https://learn.microsoft.com/visualstudio/msbuild/how-to-use-project-sdk)
- [Customize your build](https://learn.microsoft.com/visualstudio/msbuild/customize-your-build)
- [Global AnalyzerConfig files](https://github.com/dotnet/roslyn/blob/main/docs/features/editorconfig.md#global-analyzerconfig)
