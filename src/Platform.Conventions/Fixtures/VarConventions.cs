using Platform.Conventions.Rules;

namespace Platform.Conventions.Fixtures;

/// <summary>
/// The <c>var</c> standard (<see cref="VarRule"/>) over an application's production and test code. An application inherits it in one line:
/// <c>public class Var : VarConventions { }</c>.
/// </summary>
[TestFixture]
public abstract class VarConventions : ConventionFixture
{
    /// <summary>Guards the rule against a wrong path: a scan that finds no files passes by finding nothing.</summary>
    [Test]
    public void SourceFiles_AreFound()
    {
        Assert.That(All.Trees, Is.Not.Empty, "the var rule must actually be scanning the application's source");
    }

    /// <summary>No <c>var</c> hides a type the reader cannot see.</summary>
    [Test]
    public void Var_IsUsedOnlyWhereTheTypeIsNamedOnTheRightOrIsAnonymous()
    {
        IReadOnlyList<string> violations = VarRule.Violations(All);

        Assert.That(violations, Is.Empty,
            "write the type out (decimal interest = ..., not var interest = ...); var is for new T(...), a cast, as, or an anonymous type:" + Lines(violations));
    }
}
