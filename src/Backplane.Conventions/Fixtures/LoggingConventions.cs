using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Fixtures;

/// <summary>
/// How an application logs (<see cref="LoggingRule"/>) over its production code: templates, not built strings, and no personal data or secret in a placeholder. An application overrides
/// <see cref="BannedPlaceholders"/> to add names of its own.
/// </summary>
[TestFixture]
public abstract class LoggingConventions : ConventionFixture
{
    /// <summary>Placeholder names that must not appear in a log template.</summary>
    protected virtual IReadOnlyCollection<string> BannedPlaceholders => LoggingRule.DefaultBannedPlaceholders;

    /// <summary>Guards the rule against a wrong path. An application with no <c>[LoggerMessage]</c> templates overrides this to say so.</summary>
    [Test]
    public virtual void LogTemplates_AreFound()
    {
        Assert.That(LoggingRule.TemplateCount(Production), Is.GreaterThan(0), "the logging rule must actually be finding the application's LoggerMessage templates");
    }

    /// <summary>No placeholder names personal data or a secret.</summary>
    [Test]
    public void NoLogTemplate_NamesPersonalDataOrASecret()
    {
        IReadOnlyList<string> violations = LoggingRule.PersonalDataInTemplates(Production, BannedPlaceholders);

        Assert.That(violations, Is.Empty, "log an id (a request id, a user id), not the person's data or a credential; rename the placeholder or drop the value:" + Lines(violations));
    }

    /// <summary>No log call builds its message by hand.</summary>
    [Test]
    public void NoLogCall_BuildsItsMessageByHand()
    {
        IReadOnlyList<string> violations = LoggingRule.BuiltMessages(Production);

        Assert.That(violations, Is.Empty, "write a [LoggerMessage] template with named placeholders instead of interpolating or concatenating into a log call:" + Lines(violations));
    }
}
