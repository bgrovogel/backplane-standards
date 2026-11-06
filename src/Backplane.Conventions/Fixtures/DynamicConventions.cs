using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Fixtures;

/// <summary>
/// Production code does not use <c>dynamic</c> (<see cref="DynamicRule"/>). As a ratchet, an application lists the few files that still do and how many times (<see cref="Ceilings"/>);
/// the count may only fall.
/// </summary>
[TestFixture]
public abstract class DynamicConventions : ConventionFixture
{
    /// <summary>The files that may still use <c>dynamic</c>, by relative path, and how many times each. Empty by default: none may.</summary>
    protected virtual IReadOnlyDictionary<string, int> Ceilings => new Dictionary<string, int>();

    /// <summary>Guards the rule against a wrong path: there must be production files to read.</summary>
    [Test]
    public void ProductionFiles_AreFound()
    {
        Assert.That(Production.Trees, Is.Not.Empty, "the dynamic rule must actually be scanning the application's production code");
    }

    /// <summary>No file uses <c>dynamic</c> more often than its ceiling.</summary>
    [Test]
    public void NoFile_UsesDynamicMoreOftenThanItsCeiling()
    {
        IReadOnlyList<string> over = DynamicRule.OverTheirCeiling(Production, Ceilings);

        Assert.That(over, Is.Empty, "use a real type, so the compiler checks the members; read a shape that is truly unknown as JSON or a dictionary. The files listed in Ceilings may still use dynamic:" + Lines(over));
    }

    /// <summary>Every ceiling is still needed.</summary>
    [Test]
    public void EveryCeiling_IsStillNeeded()
    {
        IReadOnlyList<string> slack = DynamicRule.SlackCeilings(Production, Ceilings);

        Assert.That(slack, Is.Empty, "a ratchet only tightens; when dynamic is removed from a file, lower (or delete) its ceiling:" + Lines(slack));
    }
}
