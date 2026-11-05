using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Tests;

/// <summary>The naming rule, proved on samples that break it and samples that do not.</summary>
[TestFixture]
public class NamingRuleTests
{
    private static IReadOnlyList<string> Found(string members, IReadOnlyCollection<string>? allowed = null) =>
        NamingRule.Violations(SourceSet.OfText($"class Sample {{ {members} }}"), allowed);

    [TestCase("void Check(int a) { }", TestName = "A parameter")]
    [TestCase("void Check() { int n = 1; }", TestName = "A local")]
    [TestCase("void Check(List<int> list) { list.Select(p => p); }", TestName = "A lambda parameter")]
    [TestCase("void Check(int[] items) { foreach (int x in items) { } }", TestName = "A foreach variable")]
    [TestCase("int Do() => 1;", TestName = "A method")]
    [TestCase("int Ab { get; }", TestName = "A property")]
    [TestCase("int _f;", TestName = "A field")]
    [TestCase("enum Kind { Ab }", TestName = "An enum member")]
    [TestCase("void Check() { try { } catch (Exception e) { } }", TestName = "A catch variable")]
    public void ANameShorterThanThreeLetters_IsFound(string members)
    {
        Assert.That(Found(members), Has.Count.GreaterThanOrEqualTo(1), "a name says what the thing is");
    }

    [TestCase("void Check(int count) { }", TestName = "A parameter of three letters or more")]
    [TestCase("void Check() { int _ = 1; }", TestName = "A discard")]
    [TestCase("void Check(Func<int, int, int> f) { Func<int, int, int> g = (_, __) => 0; }", TestName = "Discards in a lambda")]
    [TestCase("int Id { get; }", TestName = "Id, which is the right name for an identifier")]
    public void ANameThatIsLongEnough_OrIsADiscardOrId_IsFine(string members)
    {
        Assert.That(Found(members.Replace("Func<int, int, int> f", "Func<int, int, int> func").Replace("g = (", "gone = (")), Is.Empty, "the name is long enough, or needs no name");
    }

    [Test]
    public void AnApplication_CanAllowAShortNameThatIsRightForIt()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Found("enum Program { Va }"), Has.Count.EqualTo(1), "Va is not allowed by default");
            Assert.That(Found("enum Program { Va }", ["Va"]), Is.Empty, "and is, once the application says it is the Department of Veterans Affairs loan");
        });
    }

    [Test]
    public void AViolation_SaysWhereItIsAndWhatItIsCalled()
    {
        IReadOnlyList<string> violations = NamingRule.Violations(SourceSet.OfText(("Folder/Loan.cs", "class C\n{\n    void Check(int a)\n    {\n    }\n}\n")));

        Assert.That(violations, Is.EqualTo(new[] { "Folder/Loan.cs:3  a" }), "a path and line that can be clicked, then the name");
    }
}
