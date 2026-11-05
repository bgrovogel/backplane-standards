using Platform.Conventions.Rules;

namespace Platform.Conventions.Tests;

/// <summary>The complexity rule's edges and wording, on methods measured by hand so each number is known.</summary>
[TestFixture]
public class ComplexityRuleDetailTests
{
    private const int MaxLines = 10;
    private const int MaxComplexity = 5;

    private static MethodSize Method(int lines, int complexity) => new("A.cs Loan.Pay/1", "A.cs:3", lines, complexity);

    private static IReadOnlyList<string> Slack(MethodSize size, Ceiling ceiling) =>
        ComplexityRule.Slack([size], new Dictionary<string, Ceiling> { [size.Key] = ceiling }, MaxLines, MaxComplexity);

    [Test]
    public void AMethodPastALimit_IsReportedWithItsSizeAgainstWhatItMayBe()
    {
        IReadOnlyList<string> found = ComplexityRule.Problems([Method(11, 2)], new Dictionary<string, Ceiling>(), MaxLines, MaxComplexity);

        Assert.That(found, Is.EqualTo(new[] { "A.cs:3  Loan.Pay/1: 11 lines (allowed 10), complexity 2 (allowed 5)" }), "where it is, which method, and both numbers against both limits");
    }

    [TestCase(10, 5, false, TestName = "Exactly at both limits is fine")]
    [TestCase(11, 5, true, TestName = "One line over")]
    [TestCase(10, 6, true, TestName = "One path over")]
    public void TheLimits_AreInclusive(int lines, int complexity, bool reported)
    {
        Assert.That(ComplexityRule.Problems([Method(lines, complexity)], new Dictionary<string, Ceiling>(), MaxLines, MaxComplexity), Has.Count.EqualTo(reported ? 1 : 0), "a method may be as big as the limit");
    }

    [Test]
    public void AMethodWithACeiling_IsHeldToTheCeilingAndNotTheLimit()
    {
        MethodSize size = Method(20, 8);
        Dictionary<string, Ceiling> ceilings = new() { [size.Key] = new Ceiling(20, 8) };

        Assert.Multiple(() =>
        {
            Assert.That(ComplexityRule.Problems([size], ceilings, MaxLines, MaxComplexity), Is.Empty, "it is within its ceiling");
            Assert.That(ComplexityRule.Problems([Method(21, 8)], ceilings, MaxLines, MaxComplexity), Has.Count.EqualTo(1), "a line over the ceiling is reported");
        });
    }

    [Test]
    public void ACeilingThatIsExactlyWhatTheMethodNeeds_IsNotSlack()
    {
        Assert.That(Slack(Method(20, 8), new Ceiling(20, 8)), Is.Empty, "a ratchet is tight when the ceiling equals the size");
    }

    [TestCase(21, 8, TestName = "A ceiling with a line to spare")]
    [TestCase(20, 9, TestName = "A ceiling with a path to spare")]
    public void ACeilingAboveWhatTheMethodNeeds_IsSlack(int lines, int complexity)
    {
        Assert.That(Slack(Method(20, 8), new Ceiling(lines, complexity)), Is.EqualTo(new[] { "A.cs Loan.Pay/1: now 20 lines and complexity 8; lower the ceiling to that, or delete it if both are within 10 and 5" }), "it says what the method is now and what to do");
    }

    [TestCase(10, 5, TestName = "At both limits")]
    [TestCase(7, 3, TestName = "Under both limits")]
    public void AMethodBackWithinBothLimits_NeedsNoCeiling(int lines, int complexity)
    {
        Assert.That(Slack(Method(lines, complexity), new Ceiling(12, 7)), Has.Count.EqualTo(1), "the ceiling can go");
    }

    [TestCase(10, 5, 10, 5, TestName = "At both limits with a ceiling at both limits")]
    [TestCase(10, 3, 10, 3, TestName = "Lines at the limit, complexity under, ceiling no looser")]
    [TestCase(7, 5, 7, 5, TestName = "Complexity at the limit, lines under, ceiling no looser")]
    public void ACeilingForAMethodWithinBothLimits_IsSlackEvenWhenItIsNoLooserThanTheMethod(int lines, int complexity, int ceilingLines, int ceilingComplexity)
    {
        Assert.That(Slack(Method(lines, complexity), new Ceiling(ceilingLines, ceilingComplexity)), Has.Count.EqualTo(1), "within both limits the method needs no ceiling at all");
    }

    [Test]
    public void AMethodStillPastOneLimit_KeepsItsCeiling()
    {
        Assert.That(Slack(Method(10, 8), new Ceiling(10, 8)), Is.Empty, "lines are within the limit but complexity is not, so the ceiling is still needed");
    }

    [Test]
    public void ACeilingForAMethodThatIsGone_IsSlackAndSaysSo()
    {
        IReadOnlyList<string> found = ComplexityRule.Slack([], new Dictionary<string, Ceiling> { ["A.cs Loan.Old/0"] = new Ceiling(20, 8) }, MaxLines, MaxComplexity);

        Assert.That(found, Is.EqualTo(new[] { "A.cs Loan.Old/0: the method is gone; delete the entry" }), "a ceiling for nothing is dead weight");
    }
}
