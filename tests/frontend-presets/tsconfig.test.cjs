const { after, before, describe, it } = require('node:test');
const assert = require('node:assert/strict');
const { execFileSync } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const base = require('@platform/frontend-presets/tsconfig.base.json');
const { makeProject, remove } = require('./helpers.cjs');

const BASE_PATH = require.resolve('@platform/frontend-presets/tsconfig.base.json');
const TSC = require.resolve('typescript/bin/tsc');

/** Compiles the text as src/sample.ts in a project that extends the preset; returns the error codes, e.g. ['TS7006']. */
function errorsOf(root, code) {
    fs.writeFileSync(path.join(root, 'src', 'sample.ts'), code);
    fs.writeFileSync(
        path.join(root, 'tsconfig.json'),
        JSON.stringify({ extends: BASE_PATH, compilerOptions: { noEmit: true, types: [] }, include: ['src/**/*.ts'] }),
    );
    try {
        execFileSync(process.execPath, [TSC, '-p', root], { encoding: 'utf8', stdio: 'pipe' });
        return [];
    } catch (error) {
        return [...String(error.stdout).matchAll(/error (TS\d+)/g)].map((match) => match[1]);
    }
}

describe('the strict tsconfig', () => {
    let root;
    before(() => (root = makeProject()));
    after(() => remove(root));

    it('turns on every strict check the applications use', () => {
        const options = base.compilerOptions;

        assert.deepEqual(
            [
                options.strict,
                options.noImplicitOverride,
                options.noPropertyAccessFromIndexSignature,
                options.noImplicitReturns,
                options.noFallthroughCasesInSwitch,
            ],
            [true, true, true, true, true],
            'each of the five is on',
        );
    });

    it('turns on the strict Angular template checks', () => {
        assert.deepEqual(
            base.angularCompilerOptions,
            { enableI18nLegacyMessageIdFormat: false, strictInjectionParameters: true, strictInputAccessModifiers: true, strictTemplates: true },
            'the compiler checks the templates as strictly as the code',
        );
    });

    it('accepts code that is typed', () => {
        assert.deepEqual(errorsOf(root, 'export const double = (value: number): number => value * 2;\n'), [], 'nothing to say');
    });

    it('fails an implicit any (strict)', () => {
        assert.deepEqual(errorsOf(root, 'export const double = (value) => value * 2;\n'), ['TS7006'], 'a parameter has no type');
    });

    it('fails a function that returns on some paths only (noImplicitReturns)', () => {
        assert.deepEqual(
            errorsOf(root, 'export function pick(flag: boolean): number | undefined {\n  if (flag) {\n    return 1;\n  }\n}\n'),
            ['TS7030'],
            'not every path returns',
        );
    });

    it('fails a switch case that falls through (noFallthroughCasesInSwitch)', () => {
        assert.deepEqual(
            errorsOf(
                root,
                'export function name(kind: number): string {\n  let result = "";\n  switch (kind) {\n    case 1:\n      result = "one";\n    case 2:\n      result += "two";\n  }\n  return result;\n}\n',
            ),
            ['TS7029'],
            'case 1 runs into case 2',
        );
    });

    it('fails a property read with dot notation from an index signature (noPropertyAccessFromIndexSignature)', () => {
        assert.deepEqual(
            errorsOf(root, 'export const read = (bag: Record<string, number>): number => bag.total;\n'),
            ['TS4111'],
            'bag["total"] says it is a lookup',
        );
    });

    it('fails an override that does not say override (noImplicitOverride)', () => {
        assert.deepEqual(
            errorsOf(root, 'class Base {\n  run(): void {}\n}\nexport class Derived extends Base {\n  run(): void {}\n}\n'),
            ['TS4114'],
            'the keyword shows the method replaces one',
        );
    });
});
