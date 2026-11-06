using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Backplane.Conventions.Rules;

/// <summary>
/// Production code never reads the machine's local clock (<c>DateTime.Now</c>, <c>DateTimeOffset.Now</c>): it differs between a laptop, a build agent and a server in another time zone, so a test of
/// anything that depends on it passes in one place and fails in another. Inject a <c>TimeProvider</c> and read the UTC time from it, so a test can fix the time. It is a ratchet: each file that still
/// reads the local clock has a ceiling, the count may only fall, and a file not listed may read it none.
/// </summary>
public static class LocalTimeRule
{
    private static readonly string[] ClockTypes = ["DateTime", "DateTimeOffset"];

    /// <summary>How many times a file reads <c>Now</c> from <c>DateTime</c> or <c>DateTimeOffset</c> (comments and strings are not code).</summary>
    /// <param name="tree">The parsed file.</param>
    /// <returns>The count.</returns>
    public static int NowCount(SyntaxTree tree) =>
        tree.GetRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>().Count(access => access.Name.Identifier.ValueText == "Now" && ClockTypes.Contains(TypeName(access.Expression)));

    /// <summary>Files that read the local clock more often than their ceiling allows.</summary>
    /// <param name="source">The production files to read.</param>
    /// <param name="ceilings">The files that may still read it, by relative path, and how many times.</param>
    /// <returns>Each file over its ceiling and how many times it reads the clock.</returns>
    public static IReadOnlyList<string> OverTheirCeiling(SourceSet source, IReadOnlyDictionary<string, int> ceilings) => CountCeilings.Over(source, NowCount, ceilings);

    /// <summary>Ceilings that are higher than the file now needs: a ratchet only tightens.</summary>
    /// <param name="source">The production files to read.</param>
    /// <param name="ceilings">The files that may still read the local clock, by relative path, and how many times.</param>
    /// <returns>Each slack ceiling and what to lower it to.</returns>
    public static IReadOnlyList<string> SlackCeilings(SourceSet source, IReadOnlyDictionary<string, int> ceilings) => CountCeilings.Slack(source, NowCount, ceilings);

    private static string TypeName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax name => name.Identifier.ValueText,
        MemberAccessExpressionSyntax qualified => qualified.Name.Identifier.ValueText,
        _ => string.Empty,
    };
}
