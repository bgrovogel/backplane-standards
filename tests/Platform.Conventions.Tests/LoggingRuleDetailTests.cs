using Platform.Conventions.Rules;

namespace Platform.Conventions.Tests;

/// <summary>The logging rule's word lists and edges: every banned name and every logging method, the ways an attribute is written, and the shape of what it reports.</summary>
[TestFixture]
public class LoggingRuleDetailTests
{
    public static IEnumerable<string> BannedNames =>
    [
        "Email", "EmailAddress", "To", "From", "Recipient", "Phone", "PhoneNumber", "Name", "FirstName", "LastName", "FullName",
        "Address", "Password", "Token", "Secret", "Credential", "Code", "Query", "QueryString", "Body", "Income", "Debts",
    ];

    public static IEnumerable<string> LogMethods => ["Log", "LogTrace", "LogDebug", "LogInformation", "LogWarning", "LogError", "LogCritical"];

    private static IReadOnlyList<string> PersonalData(string template) =>
        LoggingRule.PersonalDataInTemplates(SourceSet.OfText($"partial class Sample {{ [LoggerMessage(1, LogLevel.Information, \"{template}\")] static partial void Log(ILogger logger); }}"));

    private static IReadOnlyList<string> Built(string call) => LoggingRule.BuiltMessages(SourceSet.OfText($"class Sample {{ void Check() {{ {call}; }} }}"));

    [TestCaseSource(nameof(BannedNames))]
    public void EveryDefaultBannedName_IsFound(string name)
    {
        IReadOnlyList<string> found = PersonalData($"Sent {{{name}}} now");

        Assert.That(found, Has.Count.EqualTo(1).And.All.Contain($"{{{name}}}"), $"{name} puts a person's data or a credential into a log");
    }

    [TestCaseSource(nameof(LogMethods))]
    public void EveryLogMethod_WithABuiltMessage_IsFound(string method)
    {
        Assert.That(Built($"logger.{method}($\"sent {{id}}\")"), Has.Count.EqualTo(1), $"{method} is a log call like the others");
    }

    [Test]
    public void ACallThatIsNotALogMethod_IsNotLookedAt()
    {
        Assert.That(Built("logger.Write($\"sent {id}\")"), Is.Empty, "only Log, LogTrace ... LogCritical are log calls");
    }

    [TestCase("logger.LogError(exception, $\"failed {id}\")", TestName = "The built message is not the first argument")]
    [TestCase("logger.LogInformation(\"count \" + 1)", TestName = "A concatenation of a string and a number")]
    [TestCase("logger.LogInformation(\"sent \" + id)", TestName = "A concatenation of a string and a value")]
    public void ABuiltMessage_InAnyArgument_IsFound(string call)
    {
        Assert.That(Built(call), Has.Count.EqualTo(1), "a message built by hand is a defect wherever it sits in the call");
    }

    [TestCase("logger.LogInformation(\"fixed text\")", TestName = "A plain message")]
    [TestCase("logger.LogInformation(first + second)", TestName = "A sum with no string in it")]
    [TestCase("logger.LogInformation(\"sent {Id}\", id)", TestName = "A template with an argument")]
    public void AMessageThatIsNotBuilt_IsFine(string call)
    {
        Assert.That(Built(call), Is.Empty, "nothing is formatted by hand");
    }

    [TestCase("[LoggerMessage(1, LogLevel.Information, \"Sent {Email}\")]", TestName = "Positional")]
    [TestCase("[LoggerMessage(1, LogLevel.Information, Message = \"Sent {Email}\")]", TestName = "A named message")]
    [TestCase("[LoggerMessage(Message = \"Sent {Email}\")]", TestName = "Only a named message")]
    [TestCase("[LoggerMessageAttribute(1, LogLevel.Information, \"Sent {Email}\")]", TestName = "With the suffix")]
    [TestCase("[Microsoft.Extensions.Logging.LoggerMessage(1, LogLevel.Information, \"Sent {Email}\")]", TestName = "Qualified")]
    public void AnAttribute_InEveryWayItIsWritten_IsRead(string attribute)
    {
        IReadOnlyList<string> found = LoggingRule.PersonalDataInTemplates(SourceSet.OfText($"partial class Sample {{ {attribute} static partial void Log(ILogger logger); }}"));

        Assert.That(found, Has.Count.EqualTo(1), "the template is found however the attribute is spelled");
    }

    [TestCase("[LoggerMessage]", TestName = "No arguments")]
    [TestCase("[LoggerMessage(1, LogLevel.Information)]", TestName = "No message")]
    [TestCase("[Obsolete(\"Email\", true, \"Sent {Email}\")]", TestName = "Some other attribute")]
    [TestCase("[Microsoft.Extensions.Logging.LoggerMessageOther(1, LogLevel.Information, \"Sent {Email}\")]", TestName = "A similar name")]
    public void AnAttributeWithNoMessage_OrNotThatAttribute_IsLeftAlone(string attribute)
    {
        string code = $"partial class Sample {{ {attribute} static partial void Log(ILogger logger); }}";

        Assert.Multiple(() =>
        {
            Assert.That(LoggingRule.PersonalDataInTemplates(SourceSet.OfText(code)), Is.Empty, "no template, no placeholder to flag");
            Assert.That(LoggingRule.TemplateCount(SourceSet.OfText(code)), Is.Zero, "and it is not counted as a template");
        });
    }

    [TestCase("{Email} first", TestName = "A placeholder at the very start")]
    [TestCase("Sent {Email:N0} formatted", TestName = "With a format")]
    [TestCase("Sent {@Email} destructured", TestName = "Destructured")]
    [TestCase("Sent {$Email} stringified", TestName = "Stringified")]
    [TestCase("Sent {Email,10} aligned", TestName = "With alignment")]
    public void APlaceholder_InEveryForm_IsRead(string template)
    {
        Assert.That(PersonalData(template), Has.Count.EqualTo(1), "the name is what comes before the format, the alignment and the destructuring mark");
    }

    [Test]
    public void ANamedMessage_WinsOverAPositionalOne()
    {
        IReadOnlyList<string> found = LoggingRule.PersonalDataInTemplates(
            SourceSet.OfText("partial class Sample { [LoggerMessage(1, LogLevel.Information, \"positional {Email}\", Message = \"named {Name}\")] static partial void Log(ILogger logger); }"));

        Assert.That(found.Single(), Does.Contain("{Name}"), "the Message argument is the template when there is one");
    }

    [Test]
    public void TheDefaultList_ContainsEveryBannedName()
    {
        Assert.That(LoggingRule.DefaultBannedPlaceholders, Is.EquivalentTo(BannedNames), "the list the rule ships is the list this test pins");
    }

    [Test]
    public void ABraceThatIsNeverClosed_IsNotAPlaceholder()
    {
        Assert.That(PersonalData("Sent {Email and then nothing"), Is.Empty, "an unclosed brace ends the scan without a finding or a crash");
    }

    [Test]
    public void ALongTemplate_IsShortenedInTheReport_AndAShortOneIsNot()
    {
        string sixty = "{Email}" + new string('x', 53);
        string sixtyOne = sixty + "y";

        Assert.Multiple(() =>
        {
            Assert.That(PersonalData(sixty).Single(), Does.EndWith($"in \"{sixty}\""), "a template of 60 characters is shown whole");
            Assert.That(PersonalData(sixtyOne).Single(), Does.EndWith($"in \"{sixtyOne[..57]}...\""), "a longer one is cut to 57 characters and an ellipsis");
        });
    }

    [Test]
    public void AViolation_SaysWhereAndWhichPlaceholderAndTemplate()
    {
        IReadOnlyList<string> found = LoggingRule.PersonalDataInTemplates(SourceSet.OfText(("Mail/Sender.cs", "partial class Sample\n{\n    [LoggerMessage(1, LogLevel.Information, \"Sent to {Email}\")]\n    static partial void Sent(ILogger logger);\n}\n")));

        Assert.That(found, Is.EqualTo(new[] { "Mail/Sender.cs:3  {Email} in \"Sent to {Email}\"" }), "a path and line, the placeholder, then the template");
    }

    [Test]
    public void ABuiltMessage_SaysWhereAndShowsTheCallsFirstLine()
    {
        IReadOnlyList<string> found = LoggingRule.BuiltMessages(SourceSet.OfText(("Mail/Sender.cs", "class Sample\n{\n    void Check()\n    {\n        logger.LogInformation($\"sent {id}\");\n    }\n}\n")));

        Assert.That(found, Is.EqualTo(new[] { "Mail/Sender.cs:5  logger.LogInformation($\"sent {id}\")" }), "a path and line, then the call");
    }
}
