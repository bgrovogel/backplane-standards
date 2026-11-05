using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Tests;

/// <summary>The assertion rule, proved on samples that break it and samples that do not.</summary>
[TestFixture]
public class AssertionRuleTests
{
    private static SourceSet Method(string body) => SourceSet.OfText($"class Sample {{ void Check() {{ {body} }} }}");

    [TestCase("Assert.AreEqual(1, 1);", TestName = "AreEqual")]
    [TestCase("Assert.IsTrue(true);", TestName = "IsTrue")]
    [TestCase("Assert.Pass();", TestName = "Pass")]
    public void AnAssertionOutsideTheConstraintModel_IsFound(string body)
    {
        Assert.That(AssertionRule.OtherThanTheConstraintModel(Method(body)), Has.Count.EqualTo(1), "only Assert.That and Assert.Multiple are used");
    }

    [Test]
    public void AssertThatAndAssertMultiple_AreFine()
    {
        Assert.That(AssertionRule.OtherThanTheConstraintModel(Method("Assert.That(1, Is.EqualTo(1), \"why\"); Assert.Multiple(() => { });")), Is.Empty, "the constraint model");
    }

    [Test]
    public void AnAssertThatWithoutAMessage_IsFound_AndOneWithAMessageIsNot()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AssertionRule.WithoutAMessage(Method("Assert.That(1, Is.EqualTo(1));")), Has.Count.EqualTo(1), "two arguments are no message");
            Assert.That(AssertionRule.WithoutAMessage(Method("Assert.That(1, Is.EqualTo(1), \"why\");")), Is.Empty, "the third argument is the message");
        });
    }

    [Test]
    public void SeveralAssertions_OutsideAssertMultiple_AreFound()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AssertionRule.SeveralAssertionsNotInMultiple(Method("Assert.That(1, Is.EqualTo(1), \"a\"); Assert.That(2, Is.EqualTo(2), \"b\");")), Has.Count.EqualTo(1), "two bare assertions");
            Assert.That(AssertionRule.SeveralAssertionsNotInMultiple(Method("Assert.Multiple(() => { Assert.That(1, Is.EqualTo(1), \"a\"); Assert.That(2, Is.EqualTo(2), \"b\"); });")), Is.Empty, "inside Multiple");
            Assert.That(AssertionRule.SeveralAssertionsNotInMultiple(Method("Assert.That(1, Is.EqualTo(1), \"a\");")), Is.Empty, "one assertion needs no Multiple");
        });
    }
}
