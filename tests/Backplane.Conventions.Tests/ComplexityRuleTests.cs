using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Tests;

/// <summary>The method-size ratchet, proved on samples that break it and samples that do not.</summary>
[TestFixture]
public class ComplexityRuleTests
{
    private const int Lines = ComplexityRule.DefaultMaxLines;
    private const int Paths = ComplexityRule.DefaultMaxComplexity;

    [Test]
    public void TheMeasure_CountsLines_AndEveryKindOfBranch()
    {
        IReadOnlyList<MethodSize> found = MethodSize.InSource(SourceSet.OfText(
            "class Sample\n{\n    int Straight() => 1;\n"
            + "    int Branchy(int a, int? b, string s)\n    {\n"
            + "        if (a > 0 && b > 1 || a < -5) { return 1; }\n"
            + "        foreach (char c in s) { if (c == 'x') { break; } }\n"
            + "        for (int i = 0; i < 2; i++) { }\n"
            + "        while (a > 100) { a--; }\n"
            + "        int d = b ?? 0;\n"
            + "        d = a > 1 ? 1 : 2;\n"
            + "        switch (a) { case 1: break; case 2: break; default: break; }\n"
            + "        string t = a switch { 1 => \"one\", 2 => \"two\", _ => \"many\" };\n"
            + "        try { } catch (Exception) { }\n"
            + "        return a is 1 or 2 ? d : 0;\n    }\n}"));

        Assert.Multiple(() =>
        {
            Assert.That(found, Has.Count.EqualTo(2), "both methods are measured");
            Assert.That((found[0].Lines, found[0].Complexity), Is.EqualTo((1, 1)), "an expression-bodied method is one line with one path");
            Assert.That(found[1].Lines, Is.EqualTo(13), "from the signature to the closing brace");
            Assert.That(
                found[1].Complexity,
                Is.EqualTo(1 + 3 + 2 + 1 + 1 + 1 + 1 + 2 + 2 + 1 + 2),
                "if, &&, ||; foreach, if; for; while; ??; ?:; two case labels; two switch arms (not the discard); the catch; or, ?:");
        });
    }

    [Test]
    public void AMethodOverTheLimits_IsFound_AndAnEntryForItLetsItThrough()
    {
        MethodSize big = new("src/X.cs X.Run/0", "src/X.cs:1", Lines + 1, Paths + 1);
        Dictionary<string, Ceiling> atItsSize = new() { [big.Key] = new Ceiling(Lines + 1, Paths + 1) };

        Assert.Multiple(() =>
        {
            Assert.That(ComplexityRule.Problems([big], new Dictionary<string, Ceiling>()), Has.Count.EqualTo(1), "too long and too complex, with no ceiling");
            Assert.That(ComplexityRule.Problems([big], atItsSize), Is.Empty, "a ceiling at its size lets it through");
            Assert.That(ComplexityRule.Problems([big with { Lines = Lines + 2 }], atItsSize), Has.Count.EqualTo(1), "growing past the ceiling fails");
        });
    }

    [Test]
    public void ACeilingThatIsNoLongerNeeded_IsFound()
    {
        MethodSize small = new("src/X.cs X.Run/0", "src/X.cs:1", Lines + 5, 3);

        Assert.Multiple(() =>
        {
            Assert.That(ComplexityRule.Slack([small], new Dictionary<string, Ceiling> { [small.Key] = new Ceiling(Lines + 9, Paths) }), Has.Count.EqualTo(1), "the method shrank below its ceiling: lower it");
            Assert.That(ComplexityRule.Slack([small], new Dictionary<string, Ceiling> { [small.Key] = new Ceiling(Lines + 5, Paths) }), Is.Empty, "a ceiling at the method's size is still needed");
            Assert.That(ComplexityRule.Slack([small with { Lines = 10 }], new Dictionary<string, Ceiling> { [small.Key] = new Ceiling(Lines + 5, Paths) }), Has.Count.EqualTo(1), "back under the limit: delete the entry");
            Assert.That(ComplexityRule.Slack([], new Dictionary<string, Ceiling> { [small.Key] = new Ceiling(Lines + 5, Paths) }), Has.Count.EqualTo(1), "the method is gone: delete the entry");
        });
    }

    [Test]
    public void AnApplication_CanSetItsOwnLimits()
    {
        MethodSize middling = new("src/X.cs X.Run/0", "src/X.cs:1", 30, 4);

        Assert.Multiple(() =>
        {
            Assert.That(ComplexityRule.Problems([middling], new Dictionary<string, Ceiling>()), Is.Empty, "within the default limits");
            Assert.That(ComplexityRule.Problems([middling], new Dictionary<string, Ceiling>(), maxLines: 20), Has.Count.EqualTo(1), "over a stricter one");
        });
    }
}
