// Style sheets are checked with the recommended SCSS rules. no-descending-specificity is off: component styles are written next to their templates and
// often need the order they have.
module.exports = {
    extends: [require.resolve('stylelint-config-recommended-scss')],
    rules: {
        'no-descending-specificity': null,
    },
};
