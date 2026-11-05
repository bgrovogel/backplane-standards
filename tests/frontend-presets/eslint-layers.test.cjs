const { after, before, describe, it } = require('node:test');
const assert = require('node:assert/strict');
const { makeProject, remove, lint, rulesOf } = require('./helpers.cjs');

/**
 * The layers of src/app: features may use shared and core, shared may use core, core uses neither, and one feature never imports another.
 * A rule that finds nothing looks the same as a rule that works, so each case is a small import, flagged or let through as it should be.
 */
const CASES = [
    { file: 'features/calculator/tabs/a.ts', imports: '../../compare/comparison.store', flagged: true, because: 'a feature importing another feature' },
    { file: 'features/compare/c.ts', imports: '../calculator/rate/rate', flagged: true, because: 'a feature importing another feature, one level up' },
    { file: 'features/compare/c.ts', imports: '../../shared/loan-form/loan-form', flagged: false, because: 'a feature using shared/' },
    { file: 'features/compare/c.ts', imports: '../../core/state/store', flagged: false, because: 'a feature using core/' },
    { file: 'features/calculator/tabs/a.ts', imports: '../schedule-table/table', flagged: false, because: 'a feature using its own folders' },
    { file: 'shared/loan-form/d.ts', imports: '../../features/calculator/page', flagged: true, because: 'shared/ importing a feature' },
    { file: 'shared/loan-form/d.ts', imports: '../../core/forms/rules', flagged: false, because: 'shared/ using core/' },
    { file: 'shared/loan-form/d.ts', imports: '../rate-options/options', flagged: false, because: 'shared/ using shared/' },
    { file: 'core/state/e.ts', imports: '../../shared/loan-form/fields', flagged: true, because: 'core/ importing shared/' },
    { file: 'core/state/e.ts', imports: '../../features/compare/store', flagged: true, because: 'core/ importing a feature' },
    { file: 'core/state/e.ts', imports: './shared-loan.store', flagged: false, because: 'a file with "shared" in its name is not the shared/ folder' },
    {
        file: 'features/registry.ts',
        imports: './compare/comparison.store',
        flagged: false,
        because: 'the registry at the features root is the one file that knows every feature',
    },
];

describe('the layer rule', () => {
    let root;
    before(() => (root = makeProject()));
    after(() => remove(root));

    for (const { file, imports, flagged, because } of CASES) {
        it(`${flagged ? 'flags' : 'allows'} ${because}`, async () => {
            const messages = await lint(root, `src/app/${file}`, `import { thing } from '${imports}';\nexport const used = thing;\n`);

            assert.equal(rulesOf(messages).includes('no-restricted-imports'), flagged, `${file} importing ${imports}: ${JSON.stringify(messages)}`);
        });
    }

    it('names the way out in what it says', async () => {
        const [message] = await lint(root, 'src/app/features/compare/c.ts', "import { thing } from '../calculator/x';\nexport const used = thing;\n");

        assert.match(message.message, /does not import another feature.*move it down to core\/ or shared\//s, 'the message says what to do');
    });

    it('holds a feature added later to the same rule, because the list of features is read, not typed', async () => {
        const later = makeProject(['calculator', 'newcomer']);
        try {
            const messages = await lint(later, 'src/app/features/newcomer/a.ts', "import { thing } from '../calculator/x';\nexport const used = thing;\n");

            assert.ok(rulesOf(messages).includes('no-restricted-imports'), 'the new feature may not import the old one');
        } finally {
            remove(later);
        }
    });

    it('lets a named file break the rule, and only that file', async () => {
        const options = { allowCrossFeature: ['src/app/features/history/page.spec.ts'] };
        const code = "import { thing } from '../calculator/x';\nexport const used = thing;\n";

        const allowed = await lint(root, 'src/app/features/history/page.spec.ts', code, options);
        const other = await lint(root, 'src/app/features/history/other.spec.ts', code, options);

        assert.deepEqual(
            [rulesOf(allowed).includes('no-restricted-imports'), rulesOf(other).includes('no-restricted-imports')],
            [false, true],
            'the exception is one file',
        );
    });

    it('keeps the layers it is given in the order it is given', async () => {
        const options = { layers: ['kernel', 'ui'], features: 'pages' };
        const project = makeProject(['kernel', 'ui']);
        try {
            const down = await lint(project, 'src/app/ui/a.ts', "import { thing } from '../kernel/x';\nexport const used = thing;\n", options);
            const up = await lint(project, 'src/app/kernel/a.ts', "import { thing } from '../ui/x';\nexport const used = thing;\n", options);

            assert.deepEqual(
                [rulesOf(down).includes('no-restricted-imports'), rulesOf(up).includes('no-restricted-imports')],
                [false, true],
                'the lower layer is the first one named',
            );
        } finally {
            remove(project);
        }
    });
});
