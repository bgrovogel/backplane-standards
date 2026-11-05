# Platform.Conventions.Tests

Proves each rule in `Platform.Conventions` on samples, and holds this repository's own code to the same rules.

- `*RuleTests.cs` give each rule code that breaks it and code that does not, and check that a violation says where it is.
- `OurOwnCode.cs` declares `[assembly: Conventions]` and the other files are the one-line classes (`Var`, `Naming`, ...) that inherit the fixtures, so `src` and `tests` here are scanned by the very fixtures
  an application would use. The first run found real violations in this repository's own tests and fixtures.

**What does not live here:** the rules (in `src/Platform.Conventions`). **Who may depend on it:** nothing. **Run it:** `dotnet test tests/Platform.Conventions.Tests`.
