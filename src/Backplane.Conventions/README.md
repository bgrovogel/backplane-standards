# Backplane.Conventions

The conventions tests as a package (roadmap task 1.3): rules that read an application's C# with Roslyn, and its client's TypeScript as text, and fail the build when a written standard is broken.

- **`Rules/`**: one static class per rule. It takes a `SourceSet` and returns the violations, each as a clickable `path:line` and what is wrong. A rule knows nothing of NUnit, so it is proved on a sample that breaks it.

  | Rule                    | What it holds                                                                                                                                      | What an application sets                           |
  | ----------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------- |
  | `VarRule`               | `var` only where the right side names the type, or the type is anonymous                                                                           |                                                    |
  | `NamingRule`            | Every name at least three letters                                                                                                                  | `AllowedShortNames`                                |
  | `FileLayoutRule`        | One top-level type to a file, named after it; no nested classes in production code                                                                 | `GroupingFolders` (where records may share a file) |
  | `SuppressionRule`       | A `#pragma warning disable`, `[SuppressMessage]` or `[ExcludeFromCodeCoverage]` always says why                                                    |                                                    |
  | `AssertionRule`         | Only `Assert.That` and `Assert.Multiple`; every `Assert.That` has a message; several assertions sit in `Assert.Multiple`                           |                                                    |
  | `ReadmeRule`            | Every project and named folder has a README that is more than a heading                                                                            | `NamedFolders`                                     |
  | `FloatingPointRule`     | No `double` or `float` in the folders that hold money, as a ratchet                                                                                | `MoneyFolders`, `Ceilings`                         |
  | `LayerRule`             | Dependencies point inward: project references, packages and `using` directives per layer                                                           | `Layers` (what each layer may depend on)           |
  | `ComplexityRule`        | Method length and complexity capped, as a ratchet                                                                                                  | `MaxLines`, `MaxComplexity`, `Ceilings`            |
  | `LoggingRule`           | Log templates, not built strings; no personal data or secret in a placeholder                                                                      | `BannedPlaceholders`                               |
  | `VagueNamesRule`        | No type, namespace, file or folder named for nothing (`Utils`, `Helpers`, `Common`, `Manager`)                                                     | `AllowedNames`, `VagueWords`                       |
  | `ClientSuppressionRule` | In the client, an `eslint-disable` carries `-- reason`, a `@ts-expect-error` says what it expects, and `@ts-ignore` and `@ts-nocheck` are not used |                                                    |
  | `PlaywrightExpectRule`  | Every Playwright `expect(actual, 'message').toX()` has a message, found by reading the call's arguments, not by a regular expression over the line |                                                    |

- **`Fixtures/`**: one abstract NUnit fixture per rule (`VarConventions`, ...). An application inherits it in one line and gets the rule's tests, a "the scan found files" guard included.
- **`SourceSet`** finds and parses the C# files; **`TextSet`** reads the client's files as text (the two TypeScript rules take one). Both leave out build output and generated code, once per run. **`[assembly: Conventions]`** says where the repository is, and where its client is (`ClientFolders`, `ClientExtensions`, `EndToEndFolders`), found from the file that declares the attribute so it works wherever the build writes its output.

An application's conventions project:

```csharp
[assembly: Conventions]                       // one file, once

public class Var : VarConventions { }         // one class to a file, one line each
public class Naming : NamingConventions { }
```

A setting that differs per application is a virtual property the application overrides (`NamingConventions.AllowedShortNames`, `FileLayoutConventions.GroupingFolders`).

**What does not live here:** an application's own rules (the validation-code rule needs the application's `Reject` helper, so it waits for the kit), the checks that need the TypeScript compiler (`id-length`, import boundaries, which belong to the front-end presets, roadmap 1.5), and
anything that needs the application's types. **Who may depend on it:** an application's
conventions test project, as a package. **Tests:** [`tests/Backplane.Conventions.Tests`](../../tests/Backplane.Conventions.Tests/README.md), which also runs every fixture on this repository's own code.
**Read more:** [Roslyn conventions tests](../../docs/primers/roslyn-conventions-tests.md), [Quality gates, G-08](../../docs/03-quality-gates.md).
