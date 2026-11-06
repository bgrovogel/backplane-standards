using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Backplane.Conventions.Rules;

/// <summary>
/// Production code never uses <c>dynamic</c>: it moves type checking from the compiler to the running program, so a misspelt member is found by a user, not by the build. Use a real type; where a shape
/// is truly unknown, read it as JSON or a dictionary. It is a ratchet: each file that still uses it has a ceiling, the count may only fall, and a file not listed may use none.
/// </summary>
public static class DynamicRule
{
    /// <summary>How many times a file names the type <c>dynamic</c> (comments, strings and longer names are not it).</summary>
    /// <param name="tree">The parsed file.</param>
    /// <returns>The count.</returns>
    public static int KeywordCount(SyntaxTree tree) => tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>().Count(name => name.Identifier.ValueText == "dynamic");

    /// <summary>Files that use <c>dynamic</c> more often than their ceiling allows.</summary>
    /// <param name="source">The production files to read.</param>
    /// <param name="ceilings">The files that may still use it, by relative path, and how many times.</param>
    /// <returns>Each file over its ceiling and how many times it uses it.</returns>
    public static IReadOnlyList<string> OverTheirCeiling(SourceSet source, IReadOnlyDictionary<string, int> ceilings) => CountCeilings.Over(source, KeywordCount, ceilings);

    /// <summary>Ceilings that are higher than the file now needs: a ratchet only tightens.</summary>
    /// <param name="source">The production files to read.</param>
    /// <param name="ceilings">The files that may still use it, by relative path, and how many times.</param>
    /// <returns>Each slack ceiling and what to lower it to.</returns>
    public static IReadOnlyList<string> SlackCeilings(SourceSet source, IReadOnlyDictionary<string, int> ceilings) => CountCeilings.Slack(source, KeywordCount, ceilings);
}
