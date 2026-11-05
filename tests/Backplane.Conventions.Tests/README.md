# Backplane.Conventions.Tests

Proves each rule in `Backplane.Conventions` on samples, and holds this repository's own code to the same rules.

- `*RuleTests.cs` give each rule code that breaks it and code that does not, and check that a violation says where it is.
- `OurOwnCode.cs` declares `[assembly: Conventions]` and the other files are the one-line classes (`Var`, `Naming`, ...) that inherit the fixtures, so `src` and `tests` here are scanned by the very fixtures
  an application would use. The first run found real violations in this repository's own tests and fixtures.

- `AcceptanceOnAnotherApplication.cs` is the proof that the extraction is faithful. With `BACKPLANE_ACCEPTANCE_REPO` set to a checkout of `mortgage-analytics`, every rule runs on its 300-odd source
  files with its settings and must come back clean, and the methods past a size limit must be exactly the ones its own ratchet lists. Without the variable the tests are skipped, saying why.
  `BACKPLANE_ACCEPTANCE_REPO=C:/Dev/mortgage-analytics dotnet test tests/Backplane.Conventions.Tests`.

**What does not live here:** the rules (in `src/Backplane.Conventions`). **Who may depend on it:** nothing. **Run it:** `dotnet test tests/Backplane.Conventions.Tests`.
