using Microsoft.CodeAnalysis;

namespace Backplane.Conventions.Rules;

/// <summary>
/// The ratchet that several rules share: each file that still breaks a rule has a ceiling, the count may only fall, and a file not listed may break it none. A rule says how it counts a file;
/// this says which files are over and which ceilings are now higher than they need to be.
/// </summary>
internal static class CountCeilings
{
    /// <summary>Files that hold more than their ceiling allows.</summary>
    /// <param name="source">The files to read.</param>
    /// <param name="count">How many times a file breaks the rule.</param>
    /// <param name="ceilings">The files that may still break it, by relative path, and how many times.</param>
    /// <returns>Each file over its ceiling and how many it holds.</returns>
    public static IReadOnlyList<string> Over(SourceSet source, Func<SyntaxTree, int> count, IReadOnlyDictionary<string, int> ceilings) =>
        [
            .. source.Trees
                .Select(tree => (Path: source.RelativePath(tree.FilePath), Count: count(tree)))
                .Where(file => file.Count > ceilings.GetValueOrDefault(file.Path, 0))
                .Select(file => $"{file.Path}  {file.Count} (allowed {ceilings.GetValueOrDefault(file.Path, 0)})"),
        ];

    /// <summary>Ceilings that are higher than the file now needs: a ratchet only tightens.</summary>
    /// <param name="source">The files to read.</param>
    /// <param name="count">How many times a file breaks the rule.</param>
    /// <param name="ceilings">The files that may still break it, by relative path, and how many times.</param>
    /// <returns>Each slack ceiling and what to lower it to.</returns>
    public static IReadOnlyList<string> Slack(SourceSet source, Func<SyntaxTree, int> count, IReadOnlyDictionary<string, int> ceilings) =>
        [
            .. ceilings
                .Select(ceiling => (ceiling.Key, Allowed: ceiling.Value, Used: source.Trees.Where(tree => source.RelativePath(tree.FilePath) == ceiling.Key).Sum(count)))
                .Where(file => file.Used < file.Allowed)
                .Select(file => $"{file.Key}  uses {file.Used}, allowed {file.Allowed}: lower the ceiling to {file.Used}"),
        ];
}
