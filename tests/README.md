# tests

The tests of what this repository publishes, one project per package.

| Project              | What it proves                                                                             |
| -------------------- | ------------------------------------------------------------------------------------------ |
| `Platform.Sdk.Tests` | Each build rule in `Platform.Sdk` applies: a sample project that breaks it fails the build |

**What does not live here:** the packages (in `src/`). **Who may depend on it:** nothing. **Run them:** `dotnet test`.
