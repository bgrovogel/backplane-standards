using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Tests;

/// <summary>The floating-point rule, proved on samples that break it and samples that do not.</summary>
[TestFixture]
public class FloatingPointRuleTests
{
    private static readonly string[] Money = ["src/App.Domain/"];

    [Test]
    public void TheCount_IncludesDoubleAndFloatKeywords_ButNotWordsInCommentsOrStrings()
    {
        SourceSet sample = SourceSet.OfText("class Sample { double a; float b; decimal c; /* double */ string d = \"double\"; double Mix(double f) => (double)f; }");

        Assert.That(FloatingPointRule.KeywordCount(sample.Trees[0]), Is.EqualTo(5), "a, b, the parameter f, the return type and the cast count; the comment, the string and decimal do not");
    }

    [Test]
    public void ADoubleInAMoneyFolder_IsFound_AndOneElsewhereIsNot()
    {
        SourceSet sample = SourceSet.OfText(("src/App.Domain/Rate.cs", "class Rate { double value; }"), ("src/App.Api/Dto.cs", "class Dto { double value; }"));

        IReadOnlyList<string> over = FloatingPointRule.OverTheirCeiling(sample, Money, new Dictionary<string, int>());

        Assert.That(over, Is.EqualTo(new[] { "src/App.Domain/Rate.cs  1 (allowed 0)" }), "only the folder that holds money is held to decimal");
    }

    [Test]
    public void ACeiling_AllowsThatManyKeywords_AndNoMore()
    {
        SourceSet sample = SourceSet.OfText(("src/App.Domain/Rate.cs", "class Rate { double one; double two; }"));

        Assert.Multiple(() =>
        {
            Assert.That(FloatingPointRule.OverTheirCeiling(sample, Money, new Dictionary<string, int> { ["src/App.Domain/Rate.cs"] = 2 }), Is.Empty, "two are allowed");
            Assert.That(FloatingPointRule.OverTheirCeiling(sample, Money, new Dictionary<string, int> { ["src/App.Domain/Rate.cs"] = 1 }), Has.Count.EqualTo(1), "one is not enough");
        });
    }

    [Test]
    public void ACeilingHigherThanTheFileNeeds_IsFound()
    {
        SourceSet sample = SourceSet.OfText(("src/App.Domain/Rate.cs", "class Rate { double one; }"));

        IReadOnlyList<string> slack = FloatingPointRule.SlackCeilings(sample, Money, new Dictionary<string, int> { ["src/App.Domain/Rate.cs"] = 3 });

        Assert.That(slack, Is.EqualTo(new[] { "src/App.Domain/Rate.cs  uses 1, allowed 3: lower the ceiling to 1" }), "a ratchet only tightens");
    }
}
