using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Fixtures;

/// <summary>
/// No type, namespace, file or folder is named for nothing (<see cref="VagueNamesRule"/>). An application overrides <see cref="AllowedNames"/> with the whole names that are right for it, and
/// <see cref="VagueWords"/> if it has words of its own to ban.
/// </summary>
[TestFixture]
public abstract class VagueNamesConventions : ConventionFixture
{
    /// <summary>Whole names that are right here though they contain a vague word (<c>UserManager</c>, a class of a framework the application extends).</summary>
    protected virtual IReadOnlyCollection<string> AllowedNames => [];

    /// <summary>The words that make a name vague.</summary>
    protected virtual IReadOnlyCollection<string> VagueWords => VagueNamesRule.DefaultVagueWords;

    /// <summary>Guards the rule against a wrong path.</summary>
    [Test]
    public void SourceFiles_AreFound()
    {
        Assert.That(All.Trees, Is.Not.Empty, "the vague-names rule must actually be scanning the application's source");
    }

    /// <summary>Every name says what the thing is.</summary>
    [Test]
    public void NoName_SaysNothing()
    {
        IReadOnlyList<string> violations = VagueNamesRule.Violations(All, AllowedNames, VagueWords);

        Assert.That(violations, Is.Empty, "name it for what it is (LoanSchedule, not LoanHelpers); Utils, Helpers, Common and Manager are where code goes when nobody has decided what it is:" + Lines(violations));
    }
}
