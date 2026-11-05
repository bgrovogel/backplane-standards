using Platform.Conventions.Rules;

namespace Platform.Conventions.Fixtures;

/// <summary>
/// The method-size ratchet (<see cref="ComplexityRule"/>) over an application's production code. An application overrides <see cref="Ceilings"/> with the methods that were already past a limit
/// when it adopted the rule, and may set its own limits.
/// </summary>
[TestFixture]
public abstract class ComplexityConventions : ConventionFixture
{
    /// <summary>The longest a method may be, in lines.</summary>
    protected virtual int MaxLines => ComplexityRule.DefaultMaxLines;

    /// <summary>The most paths a method may have.</summary>
    protected virtual int MaxComplexity => ComplexityRule.DefaultMaxComplexity;

    /// <summary>The methods already past a limit, by key (<c>path/File.cs Class.Method/2</c>), and what each may be today. Empty by default.</summary>
    protected virtual IReadOnlyDictionary<string, Ceiling> Ceilings => new Dictionary<string, Ceiling>();

    /// <summary>Guards the rule against a wrong path.</summary>
    [Test]
    public void Methods_AreFound()
    {
        Assert.That(MethodSize.InSource(Production), Is.Not.Empty, "the complexity rule must actually be measuring the application's methods");
    }

    /// <summary>No method is longer or more complex than its ceiling.</summary>
    [Test]
    public void NoMethod_IsLongerOrMoreComplexThanItsCeiling()
    {
        IReadOnlyList<string> violations = ComplexityRule.Problems(MethodSize.InSource(Production), Ceilings, MaxLines, MaxComplexity);

        Assert.That(violations, Is.Empty,
            $"split the method (extract a step that has a name) rather than raise a ceiling; the limits are {MaxLines} lines and complexity {MaxComplexity}:" + Lines(violations));
    }

    /// <summary>Every ceiling is still needed.</summary>
    [Test]
    public void EveryCeiling_IsStillNeeded()
    {
        IReadOnlyList<string> slack = ComplexityRule.Slack(MethodSize.InSource(Production), Ceilings, MaxLines, MaxComplexity);

        Assert.That(slack, Is.Empty, "a ratchet only tightens; lower or delete the ceiling of a method that has shrunk:" + Lines(slack));
    }
}
