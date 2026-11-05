using System.Text.RegularExpressions;

namespace Platform.Conventions.Rules;

/// <summary>
/// A rule that is switched off says why, in the client as in C# (<see cref="SuppressionRule"/>). An <c>eslint-disable</c> carries <c>-- reason</c> (ESLint's own syntax for it), a
/// <c>@ts-expect-error</c> says what it expects, and <c>@ts-ignore</c> and <c>@ts-nocheck</c> are not used: the first hides the next error even when the code is later fixed, the second hides the
/// whole file. A reason is more than a rule's name or "ok": <see cref="SuppressionRule.ShortestReason"/> characters. Read line by line, so it needs no TypeScript parser.
/// </summary>
public static partial class ClientSuppressionRule
{
    /// <summary>Every suppression in the set that gives no reason, or is never allowed.</summary>
    /// <param name="source">The files to read.</param>
    /// <returns>Where each is, as a clickable <c>path:line</c>, and what is wrong.</returns>
    public static IReadOnlyList<string> Violations(TextSet source) =>
        [
            .. source.Files.SelectMany(file => file.Text.Split('\n')
                .Select((line, index) => (Number: index + 1, Problem: ProblemWith(line)))
                .Where(found => found.Problem is not null)
                .Select(found => $"{source.RelativePath(file.Path)}:{found.Number}  {found.Problem}")),
        ];

    private static string? ProblemWith(string line)
    {
        Match disable = EslintDisable().Match(line);
        if (disable.Success)
        {
            return disable.Groups["reason"].Success && disable.Groups["reason"].Value.Trim().Length >= SuppressionRule.ShortestReason
                ? null
                : "eslint-disable needs `-- reason` after the rule";
        }

        if (TsIgnoreOrNoCheck().IsMatch(line))
        {
            return "@ts-ignore and @ts-nocheck are not used: fix the type, or use @ts-expect-error with the reason";
        }

        Match expectError = TsExpectError().Match(line);
        return expectError.Success && expectError.Groups["reason"].Value.Trim().Length < SuppressionRule.ShortestReason
            ? "@ts-expect-error needs the reason after it"
            : null;
    }

    [GeneratedRegex(@"(?://|/\*)\s*eslint-disable(?:-next-line|-line)?\b[^\r\n]*?(?: -- (?<reason>[^\r\n*]*))?\s*(?:\*/)?\s*$")]
    private static partial Regex EslintDisable();

    [GeneratedRegex(@"(?://|/\*)\s*@ts-(?:ignore|nocheck)\b")]
    private static partial Regex TsIgnoreOrNoCheck();

    [GeneratedRegex(@"(?://|/\*)\s*@ts-expect-error\b(?<reason>[^\r\n*]*)")]
    private static partial Regex TsExpectError();
}
