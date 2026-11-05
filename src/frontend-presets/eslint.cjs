// @ts-check
const eslint = require('@eslint/js');
const { defineConfig } = require('eslint/config');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');
const prettier = require('eslint-config-prettier/flat');
const fs = require('node:fs');
const path = require('node:path');

/**
 * What an application says about itself; everything else is the standard.
 * @typedef {object} Options
 * @property {string} root The application's client folder (the one that holds src/): pass __dirname.
 * @property {string} [source] Where the layers live, relative to root. Default 'src/app'.
 * @property {string} [prefix] The prefix of every component (as an element) and directive (as an attribute). Default 'app'.
 * @property {string[]} [layers] The layer folders under source, lowest first: each may import the ones before it and nothing after. Default ['core', 'shared'].
 * @property {string} [features] The folder under source with one folder per feature, above every layer; a feature imports the layers and never another feature. Default 'features'.
 * @property {string[]} [allowCrossFeature] Files, relative to root, that may import across features (a spec that builds every feature's store). Name each; keep it short.
 */

/** A no-restricted-imports setting that refuses any import whose path passes through one of these folders. */
const refuse = (folders, message) => ({ patterns: [{ group: folders.map((folder) => `**/${folder}/**`), message }] });

/** The folder names under a directory; none if it is not there yet. */
const foldersIn = (directory) =>
    fs.existsSync(directory)
        ? fs
              .readdirSync(directory, { withFileTypes: true })
              .filter((entry) => entry.isDirectory())
              .map((entry) => entry.name)
        : [];

/**
 * The ESLint flat configuration of an Angular client: the recommended sets, naming, braces, the Angular selector prefix, accessibility in
 * templates, Prettier's agreement, and the layers of src/app.
 * @param {Options} options
 */
function createEslintConfig({ root, source = 'src/app', prefix = 'app', layers = ['core', 'shared'], features = 'features', allowCrossFeature = [] }) {
    // One folder per feature: the list is read, not typed, so a new feature is held to the same rule from its first file.
    const featureNames = foldersIn(path.join(root, source, features));
    const layerRules = layers.map((layer, index) => {
        const above = [...layers.slice(index + 1), features];
        return {
            files: [`${source}/${layer}/**/*.ts`],
            rules: {
                'no-restricted-imports': [
                    'error',
                    refuse(above, `${layer}/ is below ${above.join('/ and ')}/: it must not import from there. Move the code down, or the dependency up.`),
                ],
            },
        };
    });

    return defineConfig([
        {
            files: ['**/*.ts'],
            extends: [eslint.configs.recommended, tseslint.configs.recommended, tseslint.configs.stylistic, angular.configs.tsRecommended],
            processor: angular.processInlineTemplates,
            rules: {
                // A leading underscore marks a parameter or a destructured field that is deliberately not used.
                '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_', varsIgnorePattern: '^_', ignoreRestSiblings: true }],
                // A name says what the thing is: a variable, a parameter (an arrow function's included) or a function is at least three
                // letters, payment => payment.amount, never p => p.amount. `_` marks one that is unused; `id`, `to` and `of` are words,
                // not abbreviations. Property names are not checked: many are data that is stored or sent, and renaming those changes a format.
                'id-length': ['error', { min: 3, exceptions: ['id', 'to', 'of'], exceptionPatterns: ['^_'], properties: 'never' }],
                '@angular-eslint/directive-selector': ['error', { type: 'attribute', prefix, style: 'camelCase' }],
                '@angular-eslint/component-selector': ['error', { type: 'element', prefix, style: 'kebab-case' }],
            },
        },
        {
            files: ['**/*.html'],
            extends: [angular.configs.templateRecommended, angular.configs.templateAccessibility],
            rules: {},
        },
        // Last of the shared sets, so it turns off any rule that would argue with Prettier about layout: Prettier formats, ESLint finds bugs.
        prettier,
        // After Prettier's config, which would switch it off: braces on every if, else, for and while, one statement or many. A second
        // line under a braceless if is a classic bug, and a block keeps every diff to the lines that changed.
        {
            files: ['**/*.ts'],
            rules: { curly: ['error', 'all'] },
        },
        // The layers: a layer may use the ones below it, and a feature uses them all and never another feature. When two features need the
        // same thing it moves down a layer.
        ...layerRules,
        ...featureNames.map((feature) => ({
            files: [`${source}/${features}/${feature}/**/*.ts`],
            rules: {
                'no-restricted-imports': [
                    'error',
                    refuse(
                        featureNames.filter((other) => other !== feature),
                        `A feature does not import another feature. What ${feature} needs from there is used by two features, so move it down to ${layers.join('/ or ')}/.`,
                    ),
                ],
            },
        })),
        // Named exceptions, last so they win.
        ...(allowCrossFeature.length > 0 ? [{ files: allowCrossFeature, rules: { 'no-restricted-imports': 'off' } }] : []),
    ]);
}

module.exports = { createEslintConfig };
