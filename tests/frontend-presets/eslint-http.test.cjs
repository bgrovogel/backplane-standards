const { after, before, describe, it } = require('node:test');
const assert = require('node:assert/strict');
const { makeProject, remove, lint, rulesOf } = require('./helpers.cjs');

const RULE = '@typescript-eslint/no-restricted-imports';
const HTTP = '@angular/common/http';

/**
 * A component never talks to the network: it asks a service in core/, which owns the URL, the headers and the errors. A component that injects HttpClient puts
 * the endpoint, the retry and the error handling in a screen, where they are copied and cannot be tested without it. The rule refuses the import.
 */
const CASES = [
    { file: 'features/compare/compare.component.ts', imports: `{ HttpClient } from '${HTTP}'`, flagged: true, because: 'a component importing HttpClient' },
    { file: 'shared/loan-form/loan-form.component.ts', imports: `{ HttpClient } from '${HTTP}'`, flagged: true, because: 'a shared component importing it' },
    {
        file: 'features/compare/compare.component.ts',
        imports: `{ HttpClient as Http } from '${HTTP}'`,
        flagged: true,
        because: 'a component importing it under another name',
    },
    {
        file: 'features/compare/compare.component.ts',
        imports: `* as http from '${HTTP}'`,
        flagged: true,
        because: 'a component importing the whole module, which holds HttpClient',
    },
    {
        file: 'features/compare/compare.component.ts',
        imports: `type { HttpClient } from '${HTTP}'`,
        flagged: true,
        because: 'a component importing it only as a type',
    },
    {
        file: 'features/compare/compare.component.ts',
        imports: `{ HttpErrorResponse } from '${HTTP}'`,
        flagged: false,
        because: 'a component naming an error type from the same module',
    },
    { file: 'core/services/rates.service.ts', imports: `{ HttpClient } from '${HTTP}'`, flagged: false, because: 'a service, which is where the call belongs' },
    {
        file: 'features/compare/compare.component.spec.ts',
        imports: `{ HttpClient } from '${HTTP}'`,
        flagged: false,
        because: 'a component spec, which builds the test module',
    },
];

describe('the HttpClient rule', () => {
    let root;
    before(() => (root = makeProject()));
    after(() => remove(root));

    for (const { file, imports, flagged, because } of CASES) {
        it(`${flagged ? 'flags' : 'allows'} ${because}`, async () => {
            const messages = await lint(root, `src/app/${file}`, `import ${imports};\nexport const used = 1;\n`);

            assert.equal(rulesOf(messages).includes(RULE), flagged, `${file} importing ${imports}: ${JSON.stringify(messages)}`);
        });
    }

    it('names the way out in what it says', async () => {
        const messages = await lint(
            root,
            'src/app/features/compare/compare.component.ts',
            `import { HttpClient } from '${HTTP}';\nexport const used = HttpClient;\n`,
        );
        const message = messages.find((found) => found.ruleId === RULE);

        assert.match(message.message, /service in core\//, 'the message says where the call belongs');
    });

    it('does not take the place of the layer rule in the same file', async () => {
        const messages = await lint(
            root,
            'src/app/features/compare/compare.component.ts',
            `import { HttpClient } from '${HTTP}';\nimport { thing } from '../calculator/x';\nexport const used = [HttpClient, thing];\n`,
        );

        assert.deepEqual(
            [...new Set(rulesOf(messages))].sort(),
            [RULE, 'no-restricted-imports'],
            'a component in a feature breaks both rules at once and hears about both',
        );
    });

    it('lets a named component keep using it, and only that component', async () => {
        const options = { allowHttpClient: ['src/app/features/legacy/old.component.ts'] };
        const code = `import { HttpClient } from '${HTTP}';\nexport const used = HttpClient;\n`;

        const allowed = await lint(root, 'src/app/features/legacy/old.component.ts', code, options);
        const other = await lint(root, 'src/app/features/legacy/new.component.ts', code, options);

        assert.deepEqual([rulesOf(allowed).includes(RULE), rulesOf(other).includes(RULE)], [false, true], 'the exception is one file');
    });
});
