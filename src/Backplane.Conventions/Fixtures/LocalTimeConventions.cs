using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Fixtures;

/// <summary>
/// Production code does not read the local clock (<see cref="LocalTimeRule"/>). As a ratchet, an application lists the few files that still do and how many times (<see cref="Ceilings"/>);
/// the count may only fall.
/// </summary>
[TestFixture]
public abstract class LocalTimeConventions : ConventionFixture
{
    /// <summary>The files that may still read <c>DateTime.Now</c> or <c>DateTimeOffset.Now</c>, by relative path, and how many times each. Empty by default: none may.</summary>
    protected virtual IReadOnlyDictionary<string, int> Ceilings => new Dictionary<string, int>();

    /// <summary>Guards the rule against a wrong path: there must be production files to read.</summary>
    [Test]
    public void ProductionFiles_AreFound()
    {
        Assert.That(Production.Trees, Is.Not.Empty, "the local-clock rule must actually be scanning the application's production code");
    }

    /// <summary>No file reads the local clock more often than its ceiling.</summary>
    [Test]
    public void NoFile_ReadsTheLocalClockMoreOftenThanItsCeiling()
    {
        IReadOnlyList<string> over = LocalTimeRule.OverTheirCeiling(Production, Ceilings);

        Assert.That(over, Is.Empty, "inject a TimeProvider and read GetUtcNow() from it, so a test can fix the time; the files listed in Ceilings may still read the local clock:" + Lines(over));
    }

    /// <summary>Every ceiling is still needed.</summary>
    [Test]
    public void EveryCeiling_IsStillNeeded()
    {
        IReadOnlyList<string> slack = LocalTimeRule.SlackCeilings(Production, Ceilings);

        Assert.That(slack, Is.Empty, "a ratchet only tightens; when the local clock is removed from a file, lower (or delete) its ceiling:" + Lines(slack));
    }
}
