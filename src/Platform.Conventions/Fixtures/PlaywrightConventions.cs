using Platform.Conventions.Rules;

namespace Platform.Conventions.Fixtures;

/// <summary>The assertion-message standard (<see cref="PlaywrightExpectRule"/>) over an application's Playwright end-to-end tests.</summary>
[TestFixture]
public abstract class PlaywrightConventions : ConventionFixture
{
    /// <summary>Guards the rule against a wrong path.</summary>
    [Test]
    public void EndToEndFiles_AreFound()
    {
        Assert.That(EndToEnd.Files, Is.Not.Empty, "the Playwright rule must actually be scanning the application's end-to-end tests");
    }

    /// <summary>Guards the rule against a path that holds files but no assertions.</summary>
    [Test]
    public void ExpectCalls_AreFound()
    {
        Assert.That(PlaywrightExpectRule.CallCount(EndToEnd), Is.GreaterThan(0), "the Playwright rule must actually be finding the application's expect calls");
    }

    /// <summary>Every <c>expect</c> passes a message as its second argument.</summary>
    [Test]
    public void EveryPlaywrightExpect_HasAMessage()
    {
        IReadOnlyList<string> violations = PlaywrightExpectRule.WithoutAMessage(EndToEnd);

        Assert.That(violations, Is.Empty, "write expect(actual, 'what a failure means').toX(...) so a failure explains itself:" + Lines(violations));
    }
}
