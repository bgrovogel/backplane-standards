using Platform.Conventions.Rules;

namespace Platform.Conventions.Fixtures;

/// <summary>
/// Money is <c>decimal</c> (<see cref="FloatingPointRule"/>). An application says which folders hold money (<see cref="MoneyFolders"/>) and, as a ratchet, the few files that may still
/// use a <c>double</c> and how many times (<see cref="Ceilings"/>). With no money folders the fixture checks nothing, and says so.
/// </summary>
[TestFixture]
public abstract class FloatingPointConventions : ConventionFixture
{
    /// <summary>Path prefixes, relative to the root with forward slashes, of the folders that hold money: <c>src/MyApp.Domain/</c>.</summary>
    protected abstract IReadOnlyCollection<string> MoneyFolders { get; }

    /// <summary>The files that may still use <c>double</c> or <c>float</c>, by relative path, and how many keywords each may hold. Empty by default: none may.</summary>
    protected virtual IReadOnlyDictionary<string, int> Ceilings => new Dictionary<string, int>();

    /// <summary>Guards the rule against a wrong path: the money folders must hold files.</summary>
    [Test]
    public void MoneyFiles_AreFound()
    {
        Assert.That(Production.Trees.Count(tree => MoneyFolders.Any(folder => Production.RelativePath(tree.FilePath).StartsWith(folder, StringComparison.Ordinal))), Is.GreaterThan(0),
            "the floating-point rule must actually be scanning the money folders");
    }

    /// <summary>No file in a money folder uses more floating point than its ceiling.</summary>
    [Test]
    public void NoMoneyFile_UsesMoreFloatingPointThanItsCeiling()
    {
        IReadOnlyList<string> over = FloatingPointRule.OverTheirCeiling(Production, MoneyFolders, Ceilings);

        Assert.That(over, Is.Empty, "money and rates are decimal; use a decimal power for a whole exponent, or convert a fractional power's result back to decimal in the files listed in Ceilings:" + Lines(over));
    }

    /// <summary>Every ceiling is still needed.</summary>
    [Test]
    public void EveryCeiling_IsStillNeeded()
    {
        IReadOnlyList<string> slack = FloatingPointRule.SlackCeilings(Production, MoneyFolders, Ceilings);

        Assert.That(slack, Is.Empty, "a ratchet only tightens; when floating point is removed from a file, lower (or delete) its ceiling:" + Lines(slack));
    }
}
