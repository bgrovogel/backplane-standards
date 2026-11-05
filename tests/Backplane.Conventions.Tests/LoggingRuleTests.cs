using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Tests;

/// <summary>The logging rule, proved on samples that break it and samples that do not.</summary>
[TestFixture]
public class LoggingRuleTests
{
    [Test]
    public void ATemplate_NamingPersonalData_IsFound()
    {
        SourceSet sample = SourceSet.OfText(
            "partial class Sample { [LoggerMessage(Message = \"Sent to {Email} with {Token}\")] static partial void One(ILogger logger); "
            + "[LoggerMessage(Message = \"{Method} {Path} responded {StatusCode}\")] static partial void Two(ILogger logger); "
            + "[LoggerMessage(1, LogLevel.Error, \"Reset for {Recipient}\")] static partial void Three(ILogger logger); }");

        Assert.That(LoggingRule.PersonalDataInTemplates(sample), Has.Count.EqualTo(3), "Email, Token and Recipient are reported; Method, Path and StatusCode are not");
    }

    [Test]
    public void ATemplate_WithOnlyOrdinaryPlaceholders_IsFine()
    {
        SourceSet sample = SourceSet.OfText("partial class Sample { [LoggerMessage(Message = \"{Method} {Path} in {ElapsedMilliseconds:N0} ms (request {RequestId})\")] static partial void One(ILogger logger); }");

        Assert.That(LoggingRule.PersonalDataInTemplates(sample), Is.Empty, "ids, methods, paths and timings are what a log is for");
    }

    [Test]
    public void AnApplication_CanBanNamesOfItsOwn()
    {
        SourceSet sample = SourceSet.OfText("partial class Sample { [LoggerMessage(Message = \"Borrower {Income}\")] static partial void One(ILogger logger); }");

        Assert.Multiple(() =>
        {
            Assert.That(LoggingRule.PersonalDataInTemplates(sample, ["Salary"]), Is.Empty, "Income is not banned in this list");
            Assert.That(LoggingRule.PersonalDataInTemplates(sample, ["income"]), Has.Count.EqualTo(1), "and is, without regard to case, when the application bans it");
        });
    }

    [Test]
    public void ABuiltLogMessage_IsFound_AndATemplateCallIsNot()
    {
        SourceSet sample = SourceSet.OfText(
            "class Sample { void Check(ILogger logger, string id) { logger.LogInformation($\"Saved {id}\"); logger.LogError(\"Failed \" + id); logger.LogWarning(\"Slow: {Path}\", id); } }");

        Assert.That(LoggingRule.BuiltMessages(sample), Has.Count.EqualTo(2), "interpolation and concatenation are reported; a template with an argument is not");
    }

    [Test]
    public void TheTemplateCount_CountsWhatTheScanSaw()
    {
        SourceSet sample = SourceSet.OfText("partial class Sample { [LoggerMessage(Message = \"One\")] static partial void One(ILogger logger); [LoggerMessage(Message = \"Two\")] static partial void Two(ILogger logger); }");

        Assert.That(LoggingRule.TemplateCount(sample), Is.EqualTo(2), "so a scan that found none can be told from one that found the application's");
    }
}
