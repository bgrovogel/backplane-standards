using Platform.Conventions.Rules;

namespace Platform.Conventions.Fixtures;

/// <summary>The assertion standards (<see cref="AssertionRule"/>) over an application's test code.</summary>
[TestFixture]
public abstract class AssertionConventions : ConventionFixture
{
    /// <summary>Guards the rule against a wrong path.</summary>
    [Test]
    public void TestFiles_AreFound()
    {
        Assert.That(Tests.Trees, Is.Not.Empty, "the assertion rule must actually be scanning the application's test projects");
    }

    /// <summary>Only <c>Assert.That</c> and <c>Assert.Multiple</c> are used.</summary>
    [Test]
    public void OnlyConstraintModelAssertionsAreUsed()
    {
        IReadOnlyList<string> violations = AssertionRule.OtherThanTheConstraintModel(Tests);

        Assert.That(violations, Is.Empty, "use Assert.That(actual, constraint, message), e.g. Assert.That(() => act(), Throws.TypeOf<T>(), \"why\"), instead of:" + Lines(violations));
    }

    /// <summary>Every <c>Assert.That</c> says what a failure means.</summary>
    [Test]
    public void EveryAssertThat_HasAFailureMessage()
    {
        IReadOnlyList<string> violations = AssertionRule.WithoutAMessage(Tests);

        Assert.That(violations, Is.Empty, "every Assert.That needs a message explaining what a failure means:" + Lines(violations));
    }

    /// <summary>Several assertions in a method sit in one <c>Assert.Multiple</c>.</summary>
    [Test]
    public void MethodsWithSeveralAssertions_WrapThemInAssertMultiple()
    {
        IReadOnlyList<string> violations = AssertionRule.SeveralAssertionsNotInMultiple(Tests);

        Assert.That(violations, Is.Empty, "methods with more than one Assert.That must put them all inside Assert.Multiple:" + Lines(violations));
    }
}
