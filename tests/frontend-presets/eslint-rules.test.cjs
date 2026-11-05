const { after, before, describe, it } = require('node:test');
const assert = require('node:assert/strict');
const { makeProject, remove, lint, rulesOf } = require('./helpers.cjs');

const FILE = 'src/app/core/sample.ts';

describe('the rules every file in an application is held to', () => {
    let root;
    before(() => (root = makeProject()));
    after(() => remove(root));

    const broken = async (code, rule, file = FILE, options = {}) =>
        assert.ok(rulesOf(await lint(root, file, code, options)).includes(rule), `${rule} should flag: ${code}`);
    const fine = async (code, rule, file = FILE, options = {}) =>
        assert.ok(!rulesOf(await lint(root, file, code, options)).includes(rule), `${rule} should let through: ${code}`);

    it('flags a name shorter than three letters (id-length)', async () => {
        await broken('export const p = 1;\n', 'id-length');
        await broken('export const total = [1].map((n) => n);\n', 'id-length');
    });

    it('lets `id`, `to`, `of` and an underscore through, and does not check property names', async () => {
        await fine('export const id = 1;\n', 'id-length');
        await fine('export const total = [1].map((_n) => 1);\n', 'id-length');
        await fine('export const draft = { v: 1 };\n', 'id-length');
    });

    it('flags an if without braces (curly), which Prettier would otherwise switch off', async () => {
        await broken('export function check(flag: boolean) {\n  if (flag) return 1;\n  return 2;\n}\n', 'curly');
        await fine('export function check(flag: boolean) {\n  if (flag) {\n    return 1;\n  }\n  return 2;\n}\n', 'curly');
    });

    it('flags an unused variable, unless an underscore says it is meant', async () => {
        await broken('export function run(unused: number) {\n  return 1;\n}\n', '@typescript-eslint/no-unused-vars');
        await fine('export function run(_unused: number) {\n  return 1;\n}\n', '@typescript-eslint/no-unused-vars');
    });

    it('holds a component selector to the prefix it is given', async () => {
        const component = (selector) =>
            `import { Component } from '@angular/core';\n@Component({ selector: '${selector}', template: '' })\nexport class Sample {}\n`;

        await broken(component('other-sample'), '@angular-eslint/component-selector', 'src/app/core/sample.component.ts');
        await fine(component('app-sample'), '@angular-eslint/component-selector', 'src/app/core/sample.component.ts');
        await fine(component('xyz-sample'), '@angular-eslint/component-selector', 'src/app/core/sample.component.ts', { prefix: 'xyz' });
        await broken(component('app-sample'), '@angular-eslint/component-selector', 'src/app/core/sample.component.ts', { prefix: 'xyz' });
    });

    it('holds a directive selector to the prefix, in camelCase', async () => {
        const directive = (selector) => `import { Directive } from '@angular/core';\n@Directive({ selector: '[${selector}]' })\nexport class Sample {}\n`;

        await broken(directive('otherThing'), '@angular-eslint/directive-selector', 'src/app/core/sample.directive.ts');
        await fine(directive('appThing'), '@angular-eslint/directive-selector', 'src/app/core/sample.directive.ts');
    });

    it('flags an image with no alternative text and a click on an element a keyboard cannot reach', async () => {
        const image = await lint(root, 'src/app/core/page.html', '<img src="chart.png" />\n');
        const click = await lint(root, 'src/app/core/page.html', '<div (click)="open()">Open</div>\n');
        const fineImage = await lint(root, 'src/app/core/page.html', '<img src="chart.png" alt="Balance over time" />\n');

        assert.ok(rulesOf(image).includes('@angular-eslint/template/alt-text'), 'an image needs alt text');
        assert.ok(rulesOf(click).includes('@angular-eslint/template/click-events-have-key-events'), 'a click needs a key event');
        assert.deepEqual(rulesOf(fineImage), [], 'an image with alt text is fine');
    });
});
