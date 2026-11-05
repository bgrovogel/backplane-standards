# Platform.Conventions

The conventions tests as a package (roadmap task 1.3): rules that read an application's C# with Roslyn and fail the build when a written standard is broken.

- **`Rules/`**: one static class per rule (`VarRule`, `NamingRule`, `FileLayoutRule`, `SuppressionRule`, `AssertionRule`). It takes a `SourceSet` and returns the violations, each as a clickable `path:line` and what is wrong.
  A rule knows nothing of NUnit, so it is proved on a sample that breaks it.
- **`Fixtures/`**: one abstract NUnit fixture per rule (`VarConventions`, ...). An application inherits it in one line and gets the rule's tests, a "the scan found files" guard included.
- **`SourceSet`** finds and parses the files (build output and generated code left out), once per run. **`[assembly: Conventions]`** says where the repository is, found from the file that declares the attribute so it
  works wherever the build writes its output.

An application's conventions project:

```csharp
[assembly: Conventions]                       // one file, once

public class Var : VarConventions { }         // one class to a file, one line each
public class Naming : NamingConventions { }
```

A setting that differs per application is a virtual property the application overrides (`NamingConventions.AllowedShortNames`, `FileLayoutConventions.GroupingFolders`).

**What does not live here:** an application's own rules (what its layers are, its ceilings), the TypeScript checks, and anything that needs the application's types. **Who may depend on it:** an application's
conventions test project, as a package. **Tests:** [`tests/Platform.Conventions.Tests`](../../tests/Platform.Conventions.Tests/README.md), which also runs every fixture on this repository's own code.
**Read more:** [Roslyn conventions tests](../../docs/primers/roslyn-conventions-tests.md), [Quality gates, G-08](../../docs/03-quality-gates.md).
