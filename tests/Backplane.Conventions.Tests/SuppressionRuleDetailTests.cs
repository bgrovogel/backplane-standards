using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Tests;

/// <summary>The suppression rule's edges: which spellings count, how short a reason may be, and what is reported.</summary>
[TestFixture]
public class SuppressionRuleDetailTests
{
    private static IReadOnlyList<string> Found(string code) => SuppressionRule.Violations(SourceSet.OfText(code));

    [Test]
    public void APragmaWithAReasonOnItsLine_IsFine_AndOneWithoutIsReportedWhole()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Found("#pragma warning disable CS1591 // generated code has no documentation\nclass A { }"), Is.Empty, "a reason on the line explains it");
            Assert.That(Found("#pragma warning disable CS1591\nclass A { }"), Is.EqualTo(new[] { "Sample.cs:1  #pragma warning disable CS1591" }), "without one, the line is reported");
        });
    }

    [Test]
    public void ARestoredWarning_NeedsNoReason()
    {
        Assert.That(Found("#pragma warning disable CS1591 // reasons are given on the disable\nclass A { }\n#pragma warning restore CS1591\n"), Is.Empty, "turning a rule back on is not a suppression");
    }

    [TestCase("[SuppressMessage(\"a\", \"b\", Justification = \"12345678\")]", TestName = "A justification of exactly eight characters")]
    [TestCase("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"a\", \"b\", Justification = \"it is generated code\")]", TestName = "Qualified, with a sentence")]
    [TestCase("[SuppressMessageAttribute(\"a\", \"b\", Justification = \"it is generated code\")]", TestName = "With the suffix")]
    public void AJustifiedAttribute_IsFine(string attribute)
    {
        Assert.That(Found($"{attribute}\nclass A {{ }}"), Is.Empty, "the attribute says why");
    }

    [TestCase("[SuppressMessage(\"a\", \"b\", Justification = \"short\")]", "SuppressMessage", TestName = "A reason of five characters")]
    [TestCase("[SuppressMessage(\"a\", \"b\", Justification = \"1234567\")]", "SuppressMessage", TestName = "A reason of seven characters")]
    [TestCase("[SuppressMessage(\"a\", \"b\")]", "SuppressMessage", TestName = "No justification at all")]
    [TestCase("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"a\", \"b\")]", "SuppressMessage", TestName = "Qualified, unexplained")]
    [TestCase("[SuppressMessageAttribute(\"a\", \"b\")]", "SuppressMessage", TestName = "With the suffix, unexplained")]
    [TestCase("[ExcludeFromCodeCoverage]", "ExcludeFromCodeCoverage", TestName = "Excluded from coverage with no word")]
    public void AnUnjustifiedAttribute_IsReportedByItsShortName(string attribute, string name)
    {
        Assert.That(Found($"{attribute}\nclass A {{ }}"), Is.EqualTo(new[] { $"Sample.cs:1  [{name}] has no Justification" }), "a path, a line and what is missing");
    }

    [Test]
    public void AnAttributeThatSuppressesNothing_IsLeftAlone()
    {
        Assert.That(Found("[Obsolete]\nclass A { }"), Is.Empty, "only the suppressing attributes need a reason");
    }
}
