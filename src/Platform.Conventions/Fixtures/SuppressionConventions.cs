using Platform.Conventions.Rules;

namespace Platform.Conventions.Fixtures;

/// <summary>A suppression (<c>#pragma warning disable</c>, <c>[SuppressMessage]</c>, <c>[ExcludeFromCodeCoverage]</c>) always says why (<see cref="SuppressionRule"/>).</summary>
[TestFixture]
public abstract class SuppressionConventions : ConventionFixture
{
    /// <summary>Guards the rule against a wrong path.</summary>
    [Test]
    public void SourceFiles_AreFound()
    {
        Assert.That(All.Trees, Is.Not.Empty, "the suppression rule must actually be scanning the application's source");
    }

    /// <summary>No suppression is left without a reason.</summary>
    [Test]
    public void NoCSharpSuppression_IsLeftUnexplained()
    {
        IReadOnlyList<string> violations = SuppressionRule.Violations(All);

        Assert.That(violations, Is.Empty, "give each suppression a reason: `#pragma warning disable X // why`, or Justification = \"why\" on the attribute:" + Lines(violations));
    }
}
