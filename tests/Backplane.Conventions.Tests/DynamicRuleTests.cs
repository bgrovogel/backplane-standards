using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Tests;

/// <summary>The <c>dynamic</c> rule, proved on samples that break it and samples that do not.</summary>
[TestFixture]
public class DynamicRuleTests
{
    [Test]
    public void TheCount_IncludesDynamicAsAFieldAReturnTypeAParameterATypeArgumentAndACast()
    {
        SourceSet sample = SourceSet.OfText("class Sample { dynamic a; List<dynamic> b; dynamic Run(dynamic p) => (dynamic)p; }");

        Assert.That(DynamicRule.KeywordCount(sample.Trees[0]), Is.EqualTo(5), "the field, the type argument, the return type, the parameter and the cast");
    }

    [Test]
    public void TheCount_LeavesOutCommentsStringsAndLongerNames()
    {
        SourceSet sample = SourceSet.OfText("class Sample { int dynamics; int dynamic_value; string a = \"dynamic\"; /* dynamic */ } // dynamic");

        Assert.That(DynamicRule.KeywordCount(sample.Trees[0]), Is.Zero, "prose and other identifiers are not the type");
    }

    [Test]
    public void AFileOverItsCeiling_IsFound_AndAFileWithNoCeilingMayHoldNone()
    {
        SourceSet sample = SourceSet.OfText(("src/App/Loose.cs", "class Loose { dynamic a; dynamic b; }"), ("src/App/Clean.cs", "class Clean { }"));

        Assert.Multiple(() =>
        {
            Assert.That(DynamicRule.OverTheirCeiling(sample, new Dictionary<string, int>()), Is.EqualTo(new[] { "src/App/Loose.cs  2 (allowed 0)" }), "no ceiling means none allowed, and a clean file is not listed");
            Assert.That(DynamicRule.OverTheirCeiling(sample, new Dictionary<string, int> { ["src/App/Loose.cs"] = 2 }), Is.Empty, "two are allowed");
            Assert.That(DynamicRule.OverTheirCeiling(sample, new Dictionary<string, int> { ["src/App/Loose.cs"] = 1 }), Has.Count.EqualTo(1), "one is not enough");
        });
    }

    [Test]
    public void ACeilingHigherThanTheFileNeeds_IsFound_AndATightOneIsNot()
    {
        SourceSet sample = SourceSet.OfText(("src/App/Loose.cs", "class Loose { dynamic a; }"));

        Assert.Multiple(() =>
        {
            Assert.That(DynamicRule.SlackCeilings(sample, new Dictionary<string, int> { ["src/App/Loose.cs"] = 3 }), Is.EqualTo(new[] { "src/App/Loose.cs  uses 1, allowed 3: lower the ceiling to 1" }), "a ratchet only tightens");
            Assert.That(DynamicRule.SlackCeilings(sample, new Dictionary<string, int> { ["src/App/Loose.cs"] = 1 }), Is.Empty, "a ceiling equal to the use is tight");
        });
    }
}
