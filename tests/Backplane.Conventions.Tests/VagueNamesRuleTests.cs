using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Tests;

/// <summary>The vague-names rule, proved on samples that break it and samples that do not.</summary>
[TestFixture]
public class VagueNamesRuleTests
{
    [TestCase("class StringUtils { }", "StringUtils", TestName = "A type ending in Utils")]
    [TestCase("class Helpers { }", "Helpers", TestName = "A type that is Helpers")]
    [TestCase("class LoanManager { }", "LoanManager", TestName = "A type ending in Manager")]
    [TestCase("record CommonSettings(int A);", "CommonSettings", TestName = "A record that starts with Common")]
    [TestCase("enum Misc { One }", "Misc", TestName = "An enum called Misc")]
    [TestCase("interface IHelper { }", "IHelper", TestName = "An interface called IHelper")]
    [TestCase("namespace App.Utilities { class Rate { } }", "Utilities", TestName = "A namespace called Utilities")]
    public void AVagueName_IsFound(string code, string name)
    {
        IReadOnlyList<string> found = VagueNamesRule.Violations(SourceSet.OfText(("Rate.cs", code)));

        Assert.That(found, Has.Some.Contains(name), "a name says what the thing is; Utils, Helpers, Common and Manager say nothing");
    }

    [TestCase("class Commonwealth { }", TestName = "A word that merely contains Common")]
    [TestCase("class Payment { }", TestName = "An ordinary name")]
    [TestCase("class Manifest { }", TestName = "A word that merely starts like Manager")]
    public void AnOrdinaryName_IsFine(string code)
    {
        Assert.That(VagueNamesRule.Violations(SourceSet.OfText(("Rate.cs", code))), Is.Empty, "only whole words are vague");
    }

    [Test]
    public void AFileOrAFolderWithAVagueName_IsFound()
    {
        SourceSet sample = SourceSet.OfText(("src/App/Helpers/Rate.cs", "class Rate { }"), ("src/App/Loans/StringUtils.cs", "class Rate2 { }"));

        IReadOnlyList<string> found = VagueNamesRule.Violations(sample);

        Assert.That(found, Is.EqualTo(new[] { "src/App/Helpers/Rate.cs  folder Helpers", "src/App/Loans/StringUtils.cs  file StringUtils" }), "the folder and the file say nothing either");
    }

    [Test]
    public void AnApplication_CanAllowAName_OrAddAWord()
    {
        SourceSet sample = SourceSet.OfText(("Rate.cs", "class UserManager { } class Gadget { }"));

        Assert.Multiple(() =>
        {
            Assert.That(VagueNamesRule.Violations(sample, allowed: ["UserManager"]), Is.Empty, "UserManager is Identity's own name, and the application says so");
            Assert.That(VagueNamesRule.Violations(sample, allowed: ["UserManager"], vagueWords: ["gadget"]), Has.Count.EqualTo(1), "and a word of its own can be vague too, without regard to case");
        });
    }

    [Test]
    public void AViolation_SaysWhereItIsAndWhatIsVague()
    {
        IReadOnlyList<string> found = VagueNamesRule.Violations(SourceSet.OfText(("Folder/Rate.cs", "class Sample\n{\n}\n\nclass LoanManager\n{\n}\n")));

        Assert.That(found, Is.EqualTo(new[] { "Folder/Rate.cs:5  type LoanManager" }), "a path and line that can be clicked, then what it is");
    }
}
