# MinVer

> A small NuGet package that sets a project's version number from the git tag nearest the commit being built, so nobody edits a version by hand and the version in a package always says which commit made it.

**Read time:** 8 min · **Version:** MinVer 8.0.0 (nuget.org, checked 2026-11-05; needs the .NET SDK 8.0 or later) · **Status:** adopted in [the roadmap](../11-roadmap.md) (Phase 1 and 2); not yet used in this repository, whose packages have no version of their own until they are published · **Last verified:** 2026-11-05 against the MinVer README

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) · [How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) · [What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

Every package and assembly has a version, like `1.4.0` or `1.5.0-alpha.0.3`. Normally someone types it into a project file, and sooner or later it is wrong: forgotten, copied, or the same on two different builds. **MinVer** removes the typing. Its README says it "automates the application of SemVer 2.x versions to assemblies and NuGet packages by deriving version numbers directly from Git tags and commit history". Branches do not matter; only tags do.

**Semantic versioning (SemVer)** is the rule that a version is `MAJOR.MINOR.PATCH` with an optional pre-release part after a hyphen: the major number changes when something breaks, the minor when something is added, the patch when something is fixed, and `-alpha.1` or `-rc.2` marks something not yet final.

## Why we use it

Shared libraries are versioned and consumed by several applications ([repositories and delivery](../09-repositories-and-delivery.md)). The rule there is that the version comes from a git tag, not from a file edited by hand. Then "what is in 1.4.0?" has one answer: the commit that carries the tag. Releasing is `git tag` and `git push --tags`; there is no edit, no merge conflict on a version line, and no way to publish a version that no tag explains.

Alternatives: a hand-edited `<Version>` (drifts, conflicts); a version file bumped by a script (one more thing to run and forget); GitVersion (a larger tool with many modes and settings); build numbers from CI (not tied to a release decision). MinVer does one thing: it reads the tags.

## The big idea

> **The version is a function of the git history: the nearest version tag, plus how many commits have been made since.**

## How it works inside

1. **Find the tag.** MinVer looks at the commit being built. "If the current commit has a version tag, that version is used as-is." That is a release.
2. **Otherwise look back.** It finds the latest version tag in the history before this commit. If that tag is a **release** (`1.4.0`), it takes the next patch number, adds the default pre-release identifiers and the **height**: `1.4.1-alpha.0.3`. If the tag is itself a pre-release (`1.5.0-rc.1`), it keeps it and adds the height: `1.5.0-rc.1.3`.
3. **Height** is the number of commits since that tag. The README is clear that it exists for internal testing and not for release: "the presence of height in a version signals that the version should not be released."
4. **No tag at all.** The version is `0.0.0-alpha.0` plus the height.
5. **Set the properties.** MinVer calculates `MinVerVersion` (with `MinVerMajor`, `MinVerMinor`, `MinVerPatch`, `MinVerPreRelease`, `MinVerBuildMetadata`), then fills the .NET SDK's own properties: `Version` and `PackageVersion` from the main parts and the pre-release; `AssemblyVersion` as `{major}.0.0.0`; `FileVersion` as `{major}.{minor}.{patch}.0`; `InformationalVersion` as the whole version.

```
   tag 1.4.0 --- c1 --- c2 --- c3 (build here)         ->  1.4.1-alpha.0.3
   tag 1.4.0 (build here)                              ->  1.4.0
   no tag --- c1 --- c2 (build here)                   ->  0.0.0-alpha.0.2
```

Options (MSBuild properties, set in a project or `Directory.Build.props`):

| Property                             | What it does                                                                                                          |
| ------------------------------------ | --------------------------------------------------------------------------------------------------------------------- |
| `MinVerTagPrefix`                    | Only tags with this prefix count (`v`, or `Brand.Logging/v` in a repository with several libraries); case-insensitive |
| `MinVerMinimumMajorMinor`            | The range for builds before the first tag in it (`1.0` gives `1.0.0-alpha.0`); ignored once a higher tag exists       |
| `MinVerDefaultPreReleaseIdentifiers` | Replaces `alpha.0` (for example `preview.0`)                                                                          |
| `MinVerAutoIncrement`                | Which part rises after a release tag: `patch` (default), `minor` or `major`                                           |
| `MinVerIgnoreHeight`                 | Leaves the height out, so unrelated commits do not change a library's version                                         |
| `MinVerVerbosity`                    | How much it logs: `quiet`, `minimal` (default), `normal`, `detailed`, `diagnostic`                                    |
| `MinVerSkip`                         | Turns MinVer off (for example in Debug builds)                                                                        |

## How it fits with everything else

- **CI must fetch the tags.** The README warns that GitHub Actions, Azure Pipelines and Travis CI use shallow clones by default, so the tag may be missing. For GitHub Actions set `fetch-depth: 0` on the checkout step. Without it MinVer cannot see the tag and the version comes out as `0.0.0-alpha.0...`.
- **Several libraries in one repository** each get a tag prefix: the README's example is `main-1.0.0` for one project and `ext-1.1.0` for another. Every commit still raises every project's height; `MinVerIgnoreHeight` prevents that.
- **Publishing** ([document 9](../09-repositories-and-delivery.md)): the pack step takes `PackageVersion` from MinVer, and a changelog entry says what changed and what an application must do to upgrade.
- It is a build-time tool (`developmentDependency` on nuget.org), so it adds nothing to the package's own dependencies.

## An example from our code

This repository does not use MinVer yet; `Backplane.Sdk` and `Backplane.Conventions` are built and tested but not published (board task 17). When they are, this **sketch** goes in `Directory.Build.props`, written from the README and not yet run:

```xml
<ItemGroup>
  <PackageReference Include="MinVer" PrivateAssets="all" />
</ItemGroup>
<PropertyGroup>
  <MinVerTagPrefix>v</MinVerTagPrefix>
  <MinVerMinimumMajorMinor>0.1</MinVerMinimumMajorMinor>
</PropertyGroup>
```

The first release is then `git tag v0.1.0` on the chosen commit, `git push origin v0.1.0`, and a pack from that commit makes version `0.1.0`.

## How to use it

- Add the package with `PrivateAssets="all"`, set the prefix, and build. Run `dotnet build -p:MinVerVerbosity=detailed` to see what it found and which tags it ignored.
- Release: tag the commit, push the tag, build from that commit. Never tag a commit that has not passed `check full`.
- Pre-releases: tag `1.5.0-rc.1` to make a release candidate; tag `1.5.0` when it is final.

## The rules we follow

| Rule                                                                                        | Enforced by                        |
| ------------------------------------------------------------------------------------------- | ---------------------------------- |
| A version is a git tag; nobody edits a version number by hand                               | Review; MinVer sets the property   |
| A version with height in it (`-alpha.0.3`) is never published                               | The publish step checks the tag    |
| CI checks out with full history so tags are visible                                         | The CI workflow (`fetch-depth: 0`) |
| A tag is made only on a commit that passed `check full` and CI                              | The release routine                |
| Breaking changes raise the major number, and the changelog says what an application must do | Review; changelog check            |

## What goes wrong

| Symptom                                                     | Cause                                                                                              | Fix                                                                 |
| ----------------------------------------------------------- | -------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------- |
| CI builds `0.0.0-alpha.0.N`                                 | A shallow clone: the tag is not in the history CI fetched                                          | `fetch-depth: 0`                                                    |
| My tag is ignored                                           | MinVer is skipped, the prefix is wrong, the tag is not valid SemVer, or a higher tag supersedes it | Raise `MinVerVerbosity` to `detailed`; it names the tags it ignored |
| The version is one patch higher than the tag                | That is the rule for a build after a release tag                                                   | Tag the commit you want released                                    |
| Two libraries in one repository change each other's version | Every commit raises every project's height                                                         | Give each a tag prefix and set `MinVerIgnoreHeight`                 |
| The version looks right but the assembly version is not     | `AssemblyVersion` is `{major}.0.0.0` on purpose                                                    | Use the file or informational version to see the full number        |
| A local build shows a version from a different branch       | Tags reachable from the commit decide, not the branch                                              | Check `git describe --tags`; fetch tags (`git fetch --tags`)        |

## Terms

| Term           | Meaning                                                                   |
| -------------- | ------------------------------------------------------------------------- |
| SemVer         | The `MAJOR.MINOR.PATCH-prerelease` convention and what each part promises |
| Tag            | A name attached to one commit in git; here, `v1.4.0` marks a release      |
| Height         | The number of commits since the nearest version tag                       |
| Pre-release    | The part after the hyphen; a version with one is not final                |
| RTM            | "Release to manufacturing": a final version tag with no pre-release part  |
| Shallow clone  | A copy of a repository with only recent history, which may lack the tag   |
| Build metadata | The part after `+`, ignored when versions are compared                    |

## Further reading

- [MinVer on GitHub](https://github.com/adamralph/minver): the README has the options, the FAQ (several projects in one repository, ignoring height, "why is my version tag ignored?") and the CI notes; [MinVer on nuget.org](https://www.nuget.org/packages/MinVer).
- [Semantic Versioning 2.0.0](https://semver.org/).
- Our [repositories and delivery](../09-repositories-and-delivery.md) for tags and publishing.
