# frontend-presets tests

Proves the [front-end presets](../../src/frontend-presets/README.md) the way the .NET packages are proved: a sample that breaks each rule must fail, and a real application must pass. Plain Node (`node --test`), no test framework to install.

| File                     | What it proves                                                                                                                                                                     |
| ------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `eslint-layers.test.cjs` | Each import between layers is flagged or let through as it should be, a feature added later is held to the rule, one named file may break it, and the layer order is the one given |
| `eslint-rules.test.cjs`  | `id-length`, `curly`, unused variables, the selector prefixes, and the template accessibility rules each flag a sample that breaks them                                            |
| `prettier.test.cjs`      | Quotes, line width, Angular templates (control flow, strict whitespace), shell, XML project files and `.editorconfig` are formatted as agreed                                      |
| `stylelint.test.cjs`     | An empty block, a bad colour and a bad `@extend` are flagged; nesting and variables are not                                                                                        |
| `tsconfig.test.cjs`      | Each strict setting turns a sample into the compiler error it should                                                                                                               |
| `acceptance.test.cjs`    | The presets run on a real application and find nothing, after counting enough files that an empty folder could not pass                                                            |
| `helpers.cjs`            | A throwaway Angular-shaped project and a function that lints text as if it sat in a file                                                                                           |

**Run them:** `npm run test:frontend` from the repository root. The acceptance tests are skipped, loudly, unless `BACKPLANE_ACCEPTANCE_REPO` names the application's folder (the one that holds `client/`), because CI has no such application.
**What does not live here:** the presets (in `src/`). **Who may depend on it:** nothing.
