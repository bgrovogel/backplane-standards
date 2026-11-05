// The one layout: Prettier decides it, so nobody argues about it. Plug-ins are resolved from this package, so an application needs none of its own.
// The Angular parser is used for templates; whitespace is "strict" because in HTML a space can change what is shown.
/** @type {import('prettier').Config} */
module.exports = {
    printWidth: 160,
    singleQuote: true,
    endOfLine: 'auto',
    plugins: [require.resolve('@prettier/plugin-xml'), require.resolve('prettier-plugin-sh'), require.resolve('prettier-plugin-ini')],
    overrides: [
        {
            files: ['*.html'],
            options: { parser: 'angular', htmlWhitespaceSensitivity: 'strict' },
        },
        {
            files: ['*.esproj', '*.svg'],
            options: { parser: 'xml', xmlWhitespaceSensitivity: 'strict' },
        },
        {
            files: ['.editorconfig'],
            options: { parser: 'ini', iniSpaceAroundEquals: true },
        },
    ],
};
