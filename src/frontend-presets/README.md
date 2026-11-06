# frontend-presets

The front-end standards of an Angular application as shared configuration (roadmap task 1.5): one ESLint function, one Prettier configuration, one stylelint configuration and one strict `tsconfig`. An application imports them in a line each and holds no copy to drift. The package is `@backplane/frontend-presets`; it is not yet published (roadmap task 17).

| File                 | What it gives                                                                                                                                                                                                                                            | What an application sets                                                                                                |
| -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| `eslint.cjs`         | `createEslintConfig(options)`: the recommended sets, `id-length` (three letters), `curly` (braces everywhere), unused variables with `_`, the Angular selector prefix, template accessibility, the **layer rule** and **no `HttpClient` in a component** | `root`, `prefix`, `layers`, `features`, `source`, `allowCrossFeature`, `allowHttpClient` (all but `root` have defaults) |
| `prettier.cjs`       | 160 columns, single quotes, the Angular template parser with strict whitespace, and the plug-ins for XML, shell and `.editorconfig`                                                                                                                      | Nothing                                                                                                                 |
| `stylelint.cjs`      | The recommended SCSS rules, with `no-descending-specificity` off                                                                                                                                                                                         | Nothing                                                                                                                 |
| `tsconfig.base.json` | `strict`, `noImplicitOverride`, `noPropertyAccessFromIndexSignature`, `noImplicitReturns`, `noFallthroughCasesInSwitch` and the strict Angular template checks                                                                                           | Whatever is its own: `outDir`, `paths`, `types`                                                                         |

**The layer rule.** Under `src/app`, the layers are named lowest first (`['core', 'shared']`) and `features` sits above them: a layer imports the ones below it and nothing above, and a feature imports the layers and never another feature. The list of features is read from the folder, so a new feature is held to the rule from its first file. A file that must break it (a spec that builds every feature's store) is named in `allowCrossFeature`, one file, so the exception is visible in review.

**No `HttpClient` in a component.** A file named `*.component.ts` may not import `HttpClient` from `@angular/common/http` (under any name, or by importing the whole module): the request belongs in a service in `core/`, which owns the URL, the headers and the errors. A spec is not held to it, since it builds the test module. It uses `@typescript-eslint/no-restricted-imports`, a separate rule from the layer rule's `no-restricted-imports`, so a component in a feature hears about both at once. A component that cannot move yet is named in `allowHttpClient`, one file, as a ratchet. `mortgage-analytics` has none.

An application's files:

```js
// eslint.config.js
const { createEslintConfig } = require('@backplane/frontend-presets/eslint');
module.exports = createEslintConfig({ root: __dirname });

// prettier.config.cjs
module.exports = require('@backplane/frontend-presets/prettier');

// stylelint.config.cjs
module.exports = require('@backplane/frontend-presets/stylelint');
```

```json
{ "extends": "@backplane/frontend-presets/tsconfig.base.json", "compilerOptions": { "outDir": "./dist/out-tsc" } }
```

**What does not live here:** the checks that read the client's files as text (a suppression needs its reason, a Playwright `expect` needs a message), which are rules in [`Backplane.Conventions`](../Backplane.Conventions/README.md), and an application's own rules. **Who may depend on it:** an application's client, as a package version, never by path.
**Tests:** [`tests/frontend-presets`](../../tests/frontend-presets/README.md). **Read more:** [ESLint, Prettier and stylelint](../../docs/primers/eslint-prettier-stylelint.md), [Quality gates, G-05, G-06, G-10](../../docs/03-quality-gates.md).
