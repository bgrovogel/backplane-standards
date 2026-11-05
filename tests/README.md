# tests

The tests of what this repository publishes, one project per package.

| Project                       | What it proves                                                                                        |
| ----------------------------- | ----------------------------------------------------------------------------------------------------- |
| `Backplane.Sdk.Tests`         | Each build rule in `Backplane.Sdk` applies: a sample project that breaks it fails the build           |
| `Backplane.Conventions.Tests` | Each rule in `Backplane.Conventions` finds what it should on a sample, and this repository obeys them |
| `frontend-presets`            | Each front-end preset fails a sample that breaks it, and a real application passes                    |

**What does not live here:** the packages (in `src/`). **Who may depend on it:** nothing. **Run them:** `dotnet test`, and `npm run test:frontend` for the front-end presets.
