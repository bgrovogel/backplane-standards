const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { ESLint } = require('eslint');
const { createEslintConfig } = require('@backplane/frontend-presets/eslint');

/** The features a sample project has, so the layer rule has something to separate. */
const FEATURES = ['calculator', 'compare', 'history'];

/** A throwaway Angular-shaped folder: src/app/core, shared and one folder per feature. Removed by the caller with remove(). */
function makeProject(features = FEATURES) {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'frontend-presets-'));
    for (const folder of ['core', 'shared', ...features.map((name) => `features/${name}`)]) {
        fs.mkdirSync(path.join(root, 'src', 'app', folder), { recursive: true });
    }
    return root;
}

function remove(root) {
    fs.rmSync(root, { recursive: true, force: true });
}

/** Lints text as if it sat at `file` (relative to the project root), with the preset configured by `options`; returns the messages. */
async function lint(root, file, code, options = {}) {
    const eslint = new ESLint({ cwd: root, overrideConfigFile: true, overrideConfig: createEslintConfig({ root, ...options }) });
    const [result] = await eslint.lintText(code, { filePath: path.join(root, file) });
    return result.messages;
}

/** The ids of the rules that were broken. */
const rulesOf = (messages) => messages.map((message) => message.ruleId);

module.exports = { FEATURES, makeProject, remove, lint, rulesOf };
