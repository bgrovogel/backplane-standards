using Backplane.Conventions.Fixtures;
using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Tests;

/// <summary>The logging standard, on this repository's code: it has no logging, so the guard that templates exist is turned off here.</summary>
public class Logging : LoggingConventions
{
    /// <inheritdoc />
    public override void LogTemplates_AreFound()
    {
        Assert.That(LoggingRule.TemplateCount(Production), Is.Zero, "this repository logs nothing; the day it does, delete this override so the guard applies");
    }
}
