const { describe, it } = require('node:test');
const assert = require('node:assert/strict');
const stylelint = require('stylelint');
const config = require('@platform/frontend-presets/stylelint');

/** The rules a style sheet broke. */
async function problems(code) {
    const { results } = await stylelint.lint({ code, config, customSyntax: 'postcss-scss', codeFilename: 'sample.scss' });
    return results[0].warnings.map((warning) => warning.rule);
}

describe('the stylelint preset', () => {
    it('flags an empty block and an invalid colour', async () => {
        assert.deepEqual(await problems('.a {}\n'), ['block-no-empty'], 'an empty rule does nothing');
        assert.deepEqual(await problems('.a {\n  color: #12;\n}\n'), ['color-no-invalid-hex'], 'a colour of two digits is not a colour');
    });

    it('flags a SCSS rule that SCSS itself would not accept', async () => {
        assert.deepEqual(await problems('.a {\n  @extend .b;\n}\n'), ['scss/at-extend-no-missing-placeholder'], 'extending a class, not a placeholder');
    });

    it('lets a stylesheet in order through, nesting and variables included', async () => {
        assert.deepEqual(await problems('$gap: 4px;\n.a {\n  margin: $gap;\n  &:hover {\n    color: red;\n  }\n}\n'), [], 'nothing to say');
    });

    it('does not make a stylesheet put a general selector before a specific one (no-descending-specificity is off)', async () => {
        assert.deepEqual(await problems('.a .b {\n  color: red;\n}\n.b {\n  color: blue;\n}\n'), [], 'component styles often need the order they have');
    });
});
