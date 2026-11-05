# `dotnet new` templates

> A template is a folder of ordinary files plus one small JSON file that tells `dotnet new` how to copy them and what to rename. We use it to give every new application the same starting point, with every gate already switched on.

**Read time:** 10 min · **Version:** the .NET template engine in the .NET SDK 10 (`global.json` pins 10.0.100 or later); the packaging helper is `Microsoft.TemplateEngine.Authoring.Templates` · **Status:** adopted in [the roadmap](../11-roadmap.md) (Phase 1, the `app-template`); no template is written in this repository yet (board task 18) · **Last verified:** 2026-11-05 against Microsoft Learn, ".NET templates for authors" (page dated 2026-08)

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) · [How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) · [What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

`dotnet new console` creates a console application; `dotnet new classlib` creates a library. Those are **templates** that ship with the .NET SDK. You can write your own. In the words of Microsoft's documentation, templates are "blueprints that generate projects, files, or other resources from a predefined structure": when someone runs `dotnet new <shortName>`, "the .NET template engine reads the template and produces the output in the current directory". Visual Studio's "Create a new project" dialog uses the same engine, so a template works there too.

There are three kinds, set by `tags.type` in the template's configuration: **item** templates (one or more files), **project** templates (a whole project) and **solution** templates (a solution with several projects).

## Why we use it

A new application should not start from a blank folder, or from a copy of the last application with things deleted. The `app-template` is "a solution wired to everything" the platform provides ([document 9](../09-repositories-and-delivery.md)): the build rules, the conventions tests, the documentation checks, the CI workflow, one working use case, one endpoint and one Angular page. A newcomer runs one command, changes one rule test-first, pushes, and sees every gate run. That is the exit test of [Phase 1](../11-roadmap.md).

Alternatives: a GitHub "template repository" (copies the files but cannot rename them or ask questions, and does not work offline); a script that copies and `sed`s (works until it does not, and nobody tests it); copying the last project (carries its mistakes). A template is the supported way, it is testable (create one, build it, run its tests), and it can be shipped as a NuGet package like everything else.

## The big idea

> **The template is a real, buildable project. One file, `.template.config/template.json`, says how to turn it into a new one: which name to replace, which options to ask for.**

## How it works inside

1. **Layout.** A template is a folder with the source files and a `.template.config` subfolder. "The only required file inside `.template.config` is `template.json`." The source files need no special markers, so the source project can be built, run and debugged as it stands.
2. **Identity.** `template.json` requires `identity` (a unique id), `name` (shown to users) and `shortName` (what you type after `dotnet new`). Optional: `author`, `description`, `classifications` (tags for `dotnet new list` and `search`), and `tags` for the language and the type.
3. **Renaming.** `sourceName` is a string that appears in file names and file contents; the engine replaces it with the name the user gives with `-n` or `--name` (or the directory name if none). That is how `MyApp.Api` becomes `Acme.Orders.Api` everywhere at once.
4. **Parameters (symbols).** The `symbols` section defines options. Each parameter becomes a command line switch (`--ClassName`). A symbol has a `type` (`parameter`), `datatype` (`text`, `bool`, `choice`), a `defaultValue`, a `description`, and `replaces` (text in file contents) or `fileRename` (text in file names) that it substitutes. `dotnet new <shortName> -?` lists them.
5. **More.** The engine can include files only under a condition, run post-creation actions, and localize its text; the Microsoft page sends advanced cases to the [dotnet/templating wiki](https://github.com/dotnet/templating/wiki).
6. **Install and share.** `dotnet new install ./folder/` registers a template from a folder (re-run it after editing `template.json`). A **template package** is a NuGet package marked `PackageType` `Template`, with the template folders under `content`, built from a packaging project; `dotnet new install <PackageId>` installs the latest version from the configured NuGet feeds, and `dotnet new uninstall` with no arguments lists what is installed and how to remove it.

```
   app-template/                       dotnet new install ./app-template
     .template.config/template.json    dotnet new backplane-app -n Acme.Orders
     src/MyApp.Api/...         --->    Acme.Orders/
     tests/MyApp.Api.Tests/...           src/Acme.Orders.Api/...   (every "MyApp" replaced)
```

## How it fits with everything else

- The template carries the platform: it references `Backplane.Sdk` (the build rules), has a conventions project using `Backplane.Conventions` ([MSBuild SDKs](msbuild-sdks.md), [Roslyn conventions tests](roslyn-conventions-tests.md)), the `doc-checks.json` and `scripts/check.ps1`, and a `docs/start-here.md`.
- It is **tested by using it**: a test (or a CI job) creates an application from the template and runs `check full` on it. A template that was never created from is broken the first time someone does.
- It is published as a NuGet package from the platform's feed and versioned with [MinVer](minver.md).
- The infrastructure folder of a new application is described in [AWS CDK in C#](aws-cdk-in-csharp.md).

## An example from our code

No template exists yet, so this is a **sketch** of the `template.json` of the app-template, built from the fields in Microsoft's documentation. Names use the platform's prefix, Backplane (board task 16):

```json
{
  "$schema": "https://json.schemastore.org/template",
  "author": "Platform",
  "classifications": ["Web", "API", "Angular"],
  "identity": "Backplane.AppTemplate.CSharp",
  "name": "Platform application",
  "shortName": "backplane-app",
  "sourceName": "MyApp",
  "preferNameDirectory": true,
  "tags": { "language": "C#", "type": "solution" },
  "symbols": {
    "Title": {
      "type": "parameter",
      "datatype": "text",
      "description": "The name shown in the browser tab.",
      "defaultValue": "My application",
      "replaces": "My application"
    }
  }
}
```

- `sourceName` turns `MyApp` in every file and folder name into the name the user passes.
- `preferNameDirectory` makes the engine create a folder with that name instead of writing into the current one.
- `Title` shows a parameter that replaces text inside files; it becomes `--Title`.

## How to use it

```text
dotnet new install ./app-template/            # try a template straight from its folder
dotnet new list                               # see it, with its short name and tags
dotnet new backplane-app -n Acme.Orders -?     # the options it takes (-? after the short name)
dotnet new backplane-app -n Acme.Orders        # create it
dotnet new uninstall ./app-template/          # remove it again
```

To ship it: install `Microsoft.TemplateEngine.Authoring.Templates` (`dotnet new install Microsoft.TemplateEngine.Authoring.Templates`), make a packaging project with `dotnet new templatepack -n <PackageName>`, put the template under its `content` folder, and `dotnet pack`. Only install templates from sources you trust: Microsoft's page warns that "templates can run MSBuild tasks and arbitrary code during project creation."

## The rules we follow

| Rule                                                                                         | Enforced by                           |
| -------------------------------------------------------------------------------------------- | ------------------------------------- |
| The template is a real project that builds and passes every gate as it stands                | The template's own CI                 |
| A test creates an application from the template and runs `check full` on it                  | The template's tests                  |
| The template changes when a standard changes, in the same change                             | Review; the platform board            |
| The template contains no secrets and no names of real people or customers                    | gitleaks; review                      |
| It is published as a versioned package; an application records which version it started from | The package feed; the start-here page |

## What goes wrong

| Symptom                                                        | Cause                                                             | Fix                                                                                     |
| -------------------------------------------------------------- | ----------------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| An edit to `template.json` has no effect                       | The engine reads the installed copy                               | Run `dotnet new install` on the folder again                                            |
| Some `MyApp` strings are not renamed                           | The text differs in case or is built from pieces (`"My" + "App"`) | Use the exact `sourceName` everywhere; avoid splitting it                               |
| A name changes where it must not (a package id in a lock file) | `sourceName` replaces every occurrence in every text file         | Choose a distinctive `sourceName`; the templating wiki describes how to leave files out |
| The template does not show in `dotnet new list`                | It is not installed, or `tags.type` is missing                    | Install it; check `tags` and the `identity`                                             |
| A solution template does not show in Visual Studio's dialog    | Project and solution templates do; item templates do not          | Use the command line for item templates                                                 |
| The new application does not build                             | The template drifted from the platform and nobody created from it | The test that creates and builds an application from the template                       |

## Terms

| Term             | Meaning                                                                          |
| ---------------- | -------------------------------------------------------------------------------- |
| Template         | A folder of files plus `.template.config/template.json`                          |
| Template engine  | The part of the .NET SDK that reads a template and writes the output             |
| Short name       | What you type after `dotnet new` to choose a template                            |
| Symbol           | A parameter of the template; becomes a command line switch                       |
| Template package | A NuGet package that holds one or more templates                                 |
| Post action      | A step the engine runs after creating the files (restore a project, open a file) |

## Further reading

- Microsoft Learn: [.NET templates for authors](https://learn.microsoft.com/en-us/dotnet/core/tools/templates) (structure, `template.json` fields, symbols, template packages, testing locally) and [Custom templates for dotnet new](https://learn.microsoft.com/en-us/dotnet/core/tools/custom-templates), with the tutorials it links.
- The [dotnet/templating wiki](https://github.com/dotnet/templating/wiki) for conditions, post actions and the full configuration reference; the [`template.json` schema](https://www.schemastore.org/template.json).
- [`Microsoft.TemplateEngine.Authoring.Templates` on nuget.org](https://www.nuget.org/packages/Microsoft.TemplateEngine.Authoring.Templates).
