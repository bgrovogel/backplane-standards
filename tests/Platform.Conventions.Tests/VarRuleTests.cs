using Platform.Conventions.Rules;

namespace Platform.Conventions.Tests;

/// <summary>The var rule, proved on samples that break it and samples that do not.</summary>
[TestFixture]
public class VarRuleTests
{
    private static IReadOnlyList<string> Found(string statement) =>
        VarRule.Violations(SourceSet.OfText($"class C {{ void Check(int[] items, object o) {{ {statement} }} int One() => 1; }}"));

    [TestCase("var count = items.Length;", TestName = "A property read")]
    [TestCase("var one = One();", TestName = "A method call")]
    [TestCase("var number = 42;", TestName = "A literal")]
    [TestCase("foreach (var item in items) { }", TestName = "A foreach")]
    [TestCase("int.TryParse(\"1\", out var parsed);", TestName = "An out variable")]
    public void AVarThatHidesItsType_IsFound(string statement)
    {
        Assert.That(Found(statement), Has.Count.EqualTo(1), "the reader has to go and look at the right side to learn the type");
    }

    [TestCase("var list = new List<int>();", TestName = "A new")]
    [TestCase("var array = new int[3];", TestName = "An array creation")]
    [TestCase("var cast = (string)o;", TestName = "A cast")]
    [TestCase("var safe = o as string;", TestName = "An as")]
    [TestCase("var none = default(DateTime);", TestName = "A default of a type")]
    [TestCase("var pair = new { Name = \"x\", Count = 1 };", TestName = "An anonymous type")]
    [TestCase("var names = items.Select(item => new { item }).ToList();", TestName = "A query that selects an anonymous type")]
    [TestCase("int typed = One();", TestName = "A type written out")]
    public void AVarThatNamesItsType_OrNoVarAtAll_IsFine(string statement)
    {
        Assert.That(Found(statement), Is.Empty, "the type is visible, or there is no var");
    }

    [Test]
    public void AViolation_SaysWhereItIsAndShowsTheLine()
    {
        IReadOnlyList<string> violations = VarRule.Violations(SourceSet.OfText(("Folder/Loan.cs", "class C\n{\n    void Check()\n    {\n        var x = 1;\n    }\n}\n")));

        Assert.That(violations, Is.EqualTo(new[] { "Folder/Loan.cs:5  var x = 1" }), "a path and line that can be clicked, then the declaration");
    }
}
