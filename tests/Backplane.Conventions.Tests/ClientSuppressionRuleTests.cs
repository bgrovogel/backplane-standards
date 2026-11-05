using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Tests;

/// <summary>The TypeScript suppression rule, proved on lines that break it and lines that do not.</summary>
[TestFixture]
public class ClientSuppressionRuleTests
{
    private static IReadOnlyList<string> Found(string line) => ClientSuppressionRule.Violations(TextSet.OfText(("src/a.ts", line)));

    [TestCase("// eslint-disable-next-line no-control-regex", TestName = "eslint-disable-next-line with no reason")]
    [TestCase("// eslint-disable-next-line no-control-regex -- short", TestName = "A reason of five characters")]
    [TestCase("// eslint-disable-line no-console", TestName = "eslint-disable-line with no reason")]
    [TestCase("/* eslint-disable */", TestName = "A block disabling everything")]
    [TestCase("  /* eslint-disable no-alert */  ", TestName = "A block with padding")]
    [TestCase("// eslint-disable", TestName = "A bare disable")]
    public void AnEslintDisableWithoutAReason_IsFound(string line)
    {
        Assert.That(Found(line), Is.EqualTo(new[] { "src/a.ts:1  eslint-disable needs `-- reason` after the rule" }), "ESLint's own syntax puts the reason after two hyphens");
    }

    [TestCase("// eslint-disable-next-line no-control-regex -- XML 1.0 forbids exactly these characters", TestName = "A sentence")]
    [TestCase("// eslint-disable-next-line no-alert -- 12345678", TestName = "A reason of exactly eight characters")]
    [TestCase("/* eslint-disable no-alert -- generated file, nothing to fix by hand */", TestName = "A block with a reason")]
    public void AnEslintDisableWithAReason_IsFine(string line)
    {
        Assert.That(Found(line), Is.Empty, "the next reader can tell a considered exception from a shortcut");
    }

    [TestCase("// @ts-ignore", TestName = "ts-ignore")]
    [TestCase("// @ts-ignore -- the typings are wrong here", TestName = "ts-ignore with a reason")]
    [TestCase("/* @ts-ignore */", TestName = "ts-ignore in a block")]
    [TestCase("// @ts-nocheck", TestName = "ts-nocheck")]
    public void TsIgnoreAndTsNocheck_AreNeverUsed(string line)
    {
        Assert.That(Found(line), Is.EqualTo(new[] { "src/a.ts:1  @ts-ignore and @ts-nocheck are not used: fix the type, or use @ts-expect-error with the reason" }), "the first hides the next error even when the code is fixed, the second hides the file");
    }

    [TestCase("// @ts-expect-error", TestName = "No reason")]
    [TestCase("// @ts-expect-error mock", TestName = "A reason of four characters")]
    [TestCase("// @ts-expect-error       ", TestName = "Only spaces")]
    public void ATsExpectErrorWithoutAReason_IsFound(string line)
    {
        Assert.That(Found(line), Is.EqualTo(new[] { "src/a.ts:1  @ts-expect-error needs the reason after it" }), "it says what it expects");
    }

    [TestCase("// @ts-expect-error the mock has no id on purpose, to test the guard", TestName = "A sentence")]
    [TestCase("// @ts-expect-error 12345678", TestName = "Exactly eight characters")]
    [TestCase("const NAME = 'eslint-disable is a phrase in a string only';", TestName = "The words inside a string")]
    [TestCase("const x = 1; // an ordinary comment", TestName = "An ordinary comment")]
    public void ALineWithAReasonOrNoSuppression_IsFine(string line)
    {
        Assert.That(Found(line), Is.Empty, "nothing is switched off in silence");
    }

    [Test]
    public void EveryLineIsRead_AndEachViolationNamesItsFileAndLine()
    {
        TextSet set = TextSet.OfText(
            ("src/a.ts", "const a = 1;\n// @ts-ignore\nconst b = 2;\n"),
            ("src/page.html", "<p></p>\n<p></p>\n// eslint-disable-next-line x\n"));

        Assert.That(
            ClientSuppressionRule.Violations(set),
            Is.EqualTo(new[]
            {
                "src/a.ts:2  @ts-ignore and @ts-nocheck are not used: fix the type, or use @ts-expect-error with the reason",
                "src/page.html:3  eslint-disable needs `-- reason` after the rule",
            }),
            "a path and a line that can be clicked, then what is wrong");
    }
}
