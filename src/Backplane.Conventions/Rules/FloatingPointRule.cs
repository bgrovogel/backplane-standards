using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Backplane.Conventions.Rules;

/// <summary>
/// Money and rates are <c>decimal</c>, never <c>double</c> or <c>float</c>, in the folders that hold them: a binary floating-point number cannot hold most cent values exactly, and a
/// schedule that ends a cent off is a defect. The one reason to touch a <c>double</c> is a fractional power (compound growth over a part of a year), which <c>decimal</c> has no
/// function for. It is a ratchet: each file that still uses the keyword has a ceiling, the count may only fall, and a file not listed may use none.
/// </summary>
public static class FloatingPointRule
{
    /// <summary>How many <c>double</c> and <c>float</c> keywords a file holds (comments and strings are not tokens of that kind).</summary>
    /// <param name="tree">The parsed file.</param>
    /// <returns>The count.</returns>
    public static int KeywordCount(SyntaxTree tree) =>
        tree.GetRoot().DescendantTokens().Count(token => token.IsKind(SyntaxKind.DoubleKeyword) || token.IsKind(SyntaxKind.FloatKeyword));

    /// <summary>Files in the money folders that use more floating-point keywords than their ceiling.</summary>
    /// <param name="source">The files to read.</param>
    /// <param name="moneyFolders">Path prefixes, relative to the root with forward slashes, of the folders that hold money (<c>src/App.Domain/</c>).</param>
    /// <param name="ceilings">The files that may still use some, by relative path, and how many.</param>
    /// <returns>Each file over its ceiling and how many it uses.</returns>
    public static IReadOnlyList<string> OverTheirCeiling(SourceSet source, IReadOnlyCollection<string> moneyFolders, IReadOnlyDictionary<string, int> ceilings) =>
        [
            .. InMoneyFolders(source, moneyFolders)
                .Select(tree => (Path: source.RelativePath(tree.FilePath), Count: KeywordCount(tree)))
                .Where(file => file.Count > ceilings.GetValueOrDefault(file.Path, 0))
                .Select(file => $"{file.Path}  {file.Count} (allowed {ceilings.GetValueOrDefault(file.Path, 0)})"),
        ];

    /// <summary>Ceilings that are higher than the file now needs: a ratchet only tightens.</summary>
    /// <param name="source">The files to read.</param>
    /// <param name="moneyFolders">Path prefixes of the folders that hold money.</param>
    /// <param name="ceilings">The files that may still use some, by relative path, and how many.</param>
    /// <returns>Each slack ceiling and what to lower it to.</returns>
    public static IReadOnlyList<string> SlackCeilings(SourceSet source, IReadOnlyCollection<string> moneyFolders, IReadOnlyDictionary<string, int> ceilings) =>
        [
            .. ceilings
                .Select(ceiling => (ceiling.Key, Allowed: ceiling.Value, Used: InMoneyFolders(source, moneyFolders).Where(tree => source.RelativePath(tree.FilePath) == ceiling.Key).Sum(KeywordCount)))
                .Where(file => file.Used < file.Allowed)
                .Select(file => $"{file.Key}  uses {file.Used}, allowed {file.Allowed}: lower the ceiling to {file.Used}"),
        ];

    private static IEnumerable<SyntaxTree> InMoneyFolders(SourceSet source, IReadOnlyCollection<string> moneyFolders) =>
        source.Trees.Where(tree => moneyFolders.Any(folder => source.RelativePath(tree.FilePath).StartsWith(folder, StringComparison.Ordinal)));
}
