using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Tests;

/// <summary>Edges of the smaller rules that no other test pins: ratchet boundaries, spellings and the vague-name words.</summary>
[TestFixture]
public class SmallRuleDetailTests
{
    public static IEnumerable<string> VagueWords => ["utils", "util", "utility", "utilities", "helper", "helpers", "common", "manager", "managers", "misc", "stuff"];

    [TestCaseSource(nameof(VagueWords))]
    public void EveryDefaultVagueWord_IsFound(string word)
    {
        string name = "Loan" + char.ToUpperInvariant(word[0]) + word[1..];

        Assert.That(VagueNamesRule.Violations(SourceSet.OfText($"class {name} {{ }}")), Has.Count.EqualTo(1), $"{word} says nothing");
    }

    [Test]
    public void ALeadingI_IsNotAWord_ButAnIAnywhereElseOrAloneIs()
    {
        IReadOnlyCollection<string> vague = ["i"];

        Assert.Multiple(() =>
        {
            Assert.That(VagueNamesRule.Violations(SourceSet.OfText("class IFoo { }"), vagueWords: vague), Is.Empty, "the I of an interface name is a convention, not a word");
            Assert.That(VagueNamesRule.Violations(SourceSet.OfText("class FooI { }"), vagueWords: vague), Has.Count.EqualTo(1), "an I at the end is a word");
            Assert.That(VagueNamesRule.Violations(SourceSet.OfText("class I { }"), vagueWords: vague), Has.Count.EqualTo(1), "a name that is only I is that word");
            Assert.That(VagueNamesRule.Violations(SourceSet.OfText("class ICommon { }")), Has.Count.EqualTo(1), "the word after the I still counts");
            Assert.That(VagueNamesRule.Violations(SourceSet.OfText("class DBStuff { }"), vagueWords: ["db"]), Has.Count.EqualTo(1), "an acronym that opens a name is a word like any other");
        });
    }

    [Test]
    public void TheDefaultShortNames_AreIdInBothCases()
    {
        Assert.Multiple(() =>
        {
            Assert.That(NamingRule.Violations(SourceSet.OfText("class Sample { int Id { get; } void Check(int id) { } }")), Is.Empty, "Id and id are the right names for an identifier");
            Assert.That(NamingRule.DefaultAllowedShortNames, Is.EquivalentTo(new[] { "Id", "id" }), "and nothing else is allowed by default");
        });
    }

    [Test]
    public void AParameterWithNoNameYet_IsNotReportedAsTooShort()
    {
        Assert.That(NamingRule.Violations(SourceSet.OfText("class Sample { void Check(int) { } }")), Is.Empty, "a missing name is a compile error, not a naming finding");
    }

    [Test]
    public void ACeilingEqualToTheUse_IsNotSlack_AndTwoMoneyFoldersBothCount()
    {
        SourceSet source = SourceSet.OfText(("Loans/Rate.cs", "class Rate { double a; double b; }"), ("Fees/Fee.cs", "class Fee { double c; }"));
        string[] folders = ["Loans/", "Fees/"];

        Assert.Multiple(() =>
        {
            Assert.That(FloatingPointRule.SlackCeilings(source, folders, new Dictionary<string, int> { ["Loans/Rate.cs"] = 2 }), Is.Empty, "a ceiling that equals the count is tight");
            Assert.That(FloatingPointRule.SlackCeilings(source, folders, new Dictionary<string, int> { ["Loans/Rate.cs"] = 3 }), Has.Count.EqualTo(1), "one above is slack");
            Assert.That(FloatingPointRule.OverTheirCeiling(source, folders, new Dictionary<string, int>()), Has.Count.EqualTo(2), "a file in either folder is measured");
        });
    }

    [Test]
    public void OneAssertOutsideAssertMultiple_AlongsideOneInside_IsReported()
    {
        const string code = "class T { void Check() { Assert.That(1, Is.EqualTo(1), \"a\"); Assert.Multiple(() => { Assert.That(2, Is.EqualTo(2), \"b\"); }); } }";

        Assert.That(AssertionRule.SeveralAssertionsNotInMultiple(SourceSet.OfText(code)), Is.EqualTo(new[] { "Sample.cs:1  Check" }), "any assertion outside the group breaks the rule, and the report names the method");
    }

    [Test]
    public void AFileWhoseNameOnlyStartsLikeItsType_IsNotNamedAfterIt()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FileLayoutRule.FilesNotNamedAfterTheirType(SourceSet.OfText(("LoanOther.cs", "class Loan { }"))), Has.Count.EqualTo(1), "LoanOther.cs is not Loan.cs");
            Assert.That(FileLayoutRule.FilesNotNamedAfterTheirType(SourceSet.OfText(("Loan.Part.cs", "partial class Loan { }"))), Is.Empty, "Loan.Part.cs is");
        });
    }
}
