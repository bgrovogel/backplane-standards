namespace Platform.Conventions.Rules;

/// <summary>
/// A method that is long, or has many paths through it, is hard to read and hard to test. Two limits apply to every method and constructor: a number of lines and a cyclomatic complexity.
/// The code that already went past them is listed in a set of ceilings with its size today: a ratchet, so it can only shrink. Splitting a method (or removing a branch) makes its ceiling slack,
/// and the rule then asks for the ceiling to be lowered or deleted; a new method over a limit has no entry and fails.
/// </summary>
public static class ComplexityRule
{
    /// <summary>The longest a method may be, in lines, unless an application sets its own.</summary>
    public const int DefaultMaxLines = 50;

    /// <summary>The most paths a method may have, unless an application sets its own.</summary>
    public const int DefaultMaxComplexity = 12;

    /// <summary>The methods over a limit with no ceiling that covers them.</summary>
    /// <param name="sizes">The methods, measured.</param>
    /// <param name="ceilings">What the methods already past a limit may be, by key.</param>
    /// <param name="maxLines">The line limit.</param>
    /// <param name="maxComplexity">The complexity limit.</param>
    /// <returns>Where each is, and its size against what it is allowed.</returns>
    public static IReadOnlyList<string> Problems(IEnumerable<MethodSize> sizes, IReadOnlyDictionary<string, Ceiling> ceilings, int maxLines = DefaultMaxLines, int maxComplexity = DefaultMaxComplexity) =>
        [
            .. sizes
                .Select(size => (Size: size, Ceiling: ceilings.GetValueOrDefault(size.Key, new Ceiling(maxLines, maxComplexity))))
                .Where(found => found.Size.Lines > found.Ceiling.Lines || found.Size.Complexity > found.Ceiling.Complexity)
                .Select(found =>
                    $"{found.Size.Location}  {found.Size.Key[(found.Size.Key.IndexOf(' ') + 1)..]}: {found.Size.Lines} lines (allowed {found.Ceiling.Lines}), "
                    + $"complexity {found.Size.Complexity} (allowed {found.Ceiling.Complexity})"),
        ];

    /// <summary>The ceilings above what their method needs, or for a method that is gone or back under both limits.</summary>
    /// <param name="sizes">The methods, measured.</param>
    /// <param name="ceilings">What the methods already past a limit may be, by key.</param>
    /// <param name="maxLines">The line limit.</param>
    /// <param name="maxComplexity">The complexity limit.</param>
    /// <returns>Each slack ceiling and what to do with it.</returns>
    public static IReadOnlyList<string> Slack(IEnumerable<MethodSize> sizes, IReadOnlyDictionary<string, Ceiling> ceilings, int maxLines = DefaultMaxLines, int maxComplexity = DefaultMaxComplexity)
    {
        Dictionary<string, MethodSize> byKey = sizes.GroupBy(size => size.Key).ToDictionary(group => group.Key, group => group.First());
        return
        [
            .. ceilings
                .Select(ceiling => (ceiling.Key, Ceiling: ceiling.Value, Size: byKey.GetValueOrDefault(ceiling.Key)))
                .Where(found => found.Size is null
                    || (found.Size.Lines <= maxLines && found.Size.Complexity <= maxComplexity)
                    || found.Ceiling.Lines > Math.Max(maxLines, found.Size.Lines)
                    || found.Ceiling.Complexity > Math.Max(maxComplexity, found.Size.Complexity))
                .Select(found => found.Size is null
                    ? $"{found.Key}: the method is gone; delete the entry"
                    : $"{found.Key}: now {found.Size.Lines} lines and complexity {found.Size.Complexity}; lower the ceiling to that, or delete it if both are within {maxLines} and {maxComplexity}"),
        ];
    }
}
