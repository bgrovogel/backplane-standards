const { after, before, describe, it } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const prettier = require('prettier');
const config = require('@backplane/frontend-presets/prettier');

// Prettier applies a configuration's per-file overrides only when it finds the configuration itself, so the preset is used the way an application
// uses it: a prettier.config.cjs that hands it on.
let folder;
before(() => {
    folder = fs.mkdtempSync(path.join(os.tmpdir(), 'prettier-preset-'));
    fs.writeFileSync(
        path.join(folder, 'prettier.config.cjs'),
        `module.exports = require(${JSON.stringify(require.resolve('@backplane/frontend-presets/prettier'))});
`,
    );
});
after(() => fs.rmSync(folder, { recursive: true, force: true }));

/** The text as Prettier prints it, in the file the text pretends to be. */
async function printed(text, name) {
    const filepath = path.join(folder, name);
    return prettier.format(text, { ...(await prettier.resolveConfig(filepath)), filepath });
}

/** True when Prettier would leave the text exactly as it is. */
const isFormatted = async (text, name) => (await printed(text, name)) === text;

describe('the Prettier preset', () => {
    it('says what the team agreed: 160 columns, single quotes', () => {
        assert.equal(config.printWidth, 160, 'a line is up to 160 columns');
        assert.equal(config.singleQuote, true, 'single quotes');
    });

    it('flags double quotes in TypeScript and accepts single', async () => {
        assert.equal(await isFormatted('export const name = "x";\n', 'a.ts'), false, 'double quotes are rewritten');
        assert.equal(await isFormatted("export const name = 'x';\n", 'a.ts'), true, 'single quotes are left alone');
    });

    it('flags a line that fits in 160 columns but was broken, and accepts it joined', async () => {
        assert.equal(await isFormatted("export const list = [\n  'a',\n  'b',\n];\n", 'a.ts'), false, 'a short array is one line');
        assert.equal(await isFormatted("export const list = ['a', 'b'];\n", 'a.ts'), true, 'and is then left alone');
    });

    it('reads an Angular template as a template, with control flow', async () => {
        const template = '@if (open) {\n  <p>{{ title }}</p>\n}\n';

        assert.equal(await isFormatted(template, 'page.html'), true, 'the Angular parser knows @if');
        assert.equal(await isFormatted('@if (open) {\n<p>{{ title }}</p>\n}\n', 'page.html'), false, 'and indents what is inside');
    });

    it('never adds or removes a space that would change what a page shows', async () => {
        const result = await prettier.format('<p>See <a href="/x">the page</a>.</p>\n', { ...config, filepath: 'page.html' });

        assert.equal(result, '<p>See <a href="/x">the page</a>.</p>\n', 'no space is put between the link and the full stop');
    });

    it('formats the other files a front end holds: shell scripts, project files, the editorconfig', async () => {
        assert.equal(await isFormatted('echo   hi\n', 'run.sh'), false, 'shell, by its plugin');
        assert.equal(await isFormatted('<Project><PropertyGroup/></Project>\n', 'client.esproj'), false, 'a project file, as XML');
        assert.equal(await isFormatted('root=true\n', '.editorconfig'), false, 'the editorconfig puts spaces around =');
        assert.equal(await isFormatted('root = true\n', '.editorconfig'), true, 'and then is left alone');
    });
});
