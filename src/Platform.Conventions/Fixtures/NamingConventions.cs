using Platform.Conventions.Rules;

namespace Platform.Conventions.Fixtures;

/// <summary>
/// The naming standard (<see cref="NamingRule"/>) over an application's production and test code. An application inherits it in one line, and overrides
/// <see cref="AllowedShortNames"/> if it has short names that are right (a wire field, a loan type).
/// </summary>
[TestFixture]
public abstract class NamingConventions : ConventionFixture
{
    /// <summary>The short names that are allowed in this application.</summary>
    protected virtual IReadOnlyCollection<string> AllowedShortNames => NamingRule.DefaultAllowedShortNames;

    /// <summary>Guards the rule against a wrong path.</summary>
    [Test]
    public void SourceFiles_AreFound()
    {
        Assert.That(All.Trees, Is.Not.Empty, "the naming rule must actually be scanning the application's source");
    }

    /// <summary>No name is shorter than three letters.</summary>
    [Test]
    public void Names_SayWhatTheyAre()
    {
        IReadOnlyList<string> violations = NamingRule.Violations(All, AllowedShortNames);

        Assert.That(violations, Is.Empty,
            $"name it after what it is, at least {NamingRule.MinimumLength} letters (payment => payment.Amount, not p => p.Amount):" + Lines(violations));
    }
}
