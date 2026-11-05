const { describe, it } = require('node:test');
const assert = require('node:assert/strict');
const { execFileSync } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { ESLint } = require('eslint');
const prettier = require('prettier');
const stylelint = require('stylelint');
const { createEslintConfig } = require('@platform/frontend-presets/eslint');
const PRETTIER_CONFIG = require.resolve('@platform/frontend-presets/prettier');
const stylelintConfig = require('@platform/frontend-presets/stylelint');

/**
 * The preset run on a real application. Set PLATFORM_ACCEPTANCE_REPO to the application's folder (the one that holds client/); without it these are
 * skipped, loudly, because CI has no such application. Each check also counts what it looked at, so a run over an empty folder cannot pass.
 */
const REPO = process.env.PLATFORM_ACCEPTANCE_REPO;
const CLIENT = REPO ? path.join(REPO, 'client') : '';
const skip = REPO ? false : 'PLATFORM_ACCEPTANCE_REPO is not set: the presets were not run on a real application';

const FEATURES_ALLOWED_TO_CROSS = ['src/app/features/history/history-page.component.spec.ts'];

/** Folders an application's own .prettierignore leaves out: built, generated or third-party. */
const BUILD_OUTPUT = ['node_modules', 'dist', 'coverage', '.angular', 'reports', 'test-results', 'playwright-report', '.stryker-tmp', 'bin', 'obj', 'out-tsc'];

/** Every file under the folder with one of the extensions, leaving out build output and dependencies. */
function filesUnder(folder, extensions) {
    return fs.readdirSync(folder, { withFileTypes: true }).flatMap((entry) => {
        const full = path.join(folder, entry.name);
        if (entry.isDirectory()) {
            return BUILD_OUTPUT.includes(entry.name) ? [] : filesUnder(full, extensions);
        }
        return extensions.some((extension) => entry.name.endsWith(extension)) ? [full] : [];
    });
}

describe('the presets on a real application', { skip }, () => {
    it('lint passes: ESLint finds nothing in its TypeScript and templates', async () => {
        const eslint = new ESLint({
            cwd: CLIENT,
            overrideConfigFile: true,
            overrideConfig: createEslintConfig({ root: CLIENT, allowCrossFeature: FEATURES_ALLOWED_TO_CROSS }),
        });
        const files = filesUnder(path.join(CLIENT, 'src'), ['.ts', '.html']);

        const results = await eslint.lintFiles(files);
        const problems = results.flatMap((result) =>
            result.messages.map((message) => `${path.relative(CLIENT, result.filePath)}:${message.line} ${message.ruleId}`),
        );

        assert.ok(files.length > 100, `${files.length} files is too few to say the whole client was read`);
        assert.deepEqual(problems, [], 'the application obeys what the preset says');
    });

    it('the format check passes: Prettier would change no file', async () => {
        const files = filesUnder(CLIENT, ['.ts', '.html', '.scss', '.json']).filter((file) => !file.endsWith('package-lock.json'));
        const unformatted = [];

        for (const file of files) {
            // resolveConfig with the preset's path applies its per-file overrides; handing Prettier the object would not.
            const options = { ...(await prettier.resolveConfig(file, { config: PRETTIER_CONFIG })), filepath: file };
            if (!(await prettier.check(fs.readFileSync(file, 'utf8'), options))) {
                unformatted.push(path.relative(CLIENT, file));
            }
        }

        assert.ok(files.length > 100, `${files.length} files is too few to say the whole client was read`);
        assert.deepEqual(unformatted, [], 'every file is as Prettier prints it');
    });

    it('the style check passes: stylelint finds nothing in the SCSS', async () => {
        const files = filesUnder(path.join(CLIENT, 'src'), ['.scss']);

        const { results } = await stylelint.lint({ files, config: stylelintConfig, customSyntax: 'postcss-scss' });
        const warnings = results.flatMap((result) =>
            result.warnings.map((warning) => `${path.relative(CLIENT, result.source)}:${warning.line} ${warning.rule}`),
        );

        assert.ok(files.length > 5, `${files.length} style sheets is too few to say the client was read`);
        assert.deepEqual(warnings, [], 'the application obeys what the preset says');
    });

    it('the type check passes with the preset as the base: the application compiles as strictly as it did on its own', () => {
        const folder = fs.mkdtempSync(path.join(os.tmpdir(), 'acceptance-tsc-'));
        const file = path.join(folder, 'tsconfig.json');
        const slashed = CLIENT.replaceAll(path.sep, '/');
        fs.writeFileSync(
            file,
            JSON.stringify({
                extends: require.resolve('@platform/frontend-presets/tsconfig.base.json'),
                compilerOptions: { noEmit: true, module: 'CommonJS', types: ['jest', 'node'], typeRoots: [`${slashed}/node_modules/@types`] },
                include: [`${slashed}/src/**/*.ts`, `${slashed}/setup-jest.ts`],
            }),
        );
        try {
            execFileSync(process.execPath, [require.resolve('typescript/bin/tsc'), '-p', file], { encoding: 'utf8', stdio: 'pipe' });
        } catch (error) {
            assert.fail(`tsc failed: ${error.stdout}`);
        } finally {
            fs.rmSync(folder, { recursive: true, force: true });
        }
    });
});
