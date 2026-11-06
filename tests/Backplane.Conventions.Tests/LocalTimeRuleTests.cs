using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Tests;

/// <summary>The local-time rule, proved on samples that break it and samples that do not.</summary>
[TestFixture]
public class LocalTimeRuleTests
{
    [Test]
    public void TheCount_IncludesNowOnDateTimeAndDateTimeOffset_HoweverTheTypeIsWritten()
    {
        SourceSet sample = SourceSet.OfText("class Sample { object a = DateTime.Now; object b = System.DateTime.Now; object c = DateTimeOffset.Now; object d = System.DateTimeOffset.Now.Date; }");

        Assert.That(LocalTimeRule.NowCount(sample.Trees[0]), Is.EqualTo(4), "all four read the local clock");
    }

    [Test]
    public void TheCount_LeavesOutUtcTodayOtherClocksCommentsStringsAndLongerNames()
    {
        SourceSet sample = SourceSet.OfText("""
            class Sample
            {
                object a = DateTime.UtcNow;
                object b = DateTimeOffset.UtcNow;
                object c = clock.Now;
                object d = DateTime.NowSoon;
                object e = Other.DateTime;
                string f = "DateTime.Now"; // DateTime.Now
                /* DateTime.Now */
            }
            """);

        Assert.That(LocalTimeRule.NowCount(sample.Trees[0]), Is.Zero, "only the local clock of DateTime and DateTimeOffset is banned, and only as code");
    }

    [Test]
    public void AFileOverItsCeiling_IsFound_AndAFileWithNoCeilingMayHoldNone()
    {
        SourceSet sample = SourceSet.OfText(("src/App/Clock.cs", "class Clock { object a = DateTime.Now; object b = DateTime.Now; }"), ("src/App/Clean.cs", "class Clean { }"));

        Assert.Multiple(() =>
        {
            Assert.That(LocalTimeRule.OverTheirCeiling(sample, new Dictionary<string, int>()), Is.EqualTo(new[] { "src/App/Clock.cs  2 (allowed 0)" }), "no ceiling means none allowed, and a clean file is not listed");
            Assert.That(LocalTimeRule.OverTheirCeiling(sample, new Dictionary<string, int> { ["src/App/Clock.cs"] = 2 }), Is.Empty, "two are allowed");
            Assert.That(LocalTimeRule.OverTheirCeiling(sample, new Dictionary<string, int> { ["src/App/Clock.cs"] = 1 }), Has.Count.EqualTo(1), "one is not enough");
        });
    }

    [Test]
    public void ACeilingHigherThanTheFileNeeds_IsFound_AndATightOneIsNot()
    {
        SourceSet sample = SourceSet.OfText(("src/App/Clock.cs", "class Clock { object a = DateTime.Now; }"));

        Assert.Multiple(() =>
        {
            Assert.That(LocalTimeRule.SlackCeilings(sample, new Dictionary<string, int> { ["src/App/Clock.cs"] = 3 }), Is.EqualTo(new[] { "src/App/Clock.cs  uses 1, allowed 3: lower the ceiling to 1" }), "a ratchet only tightens");
            Assert.That(LocalTimeRule.SlackCeilings(sample, new Dictionary<string, int> { ["src/App/Clock.cs"] = 1 }), Is.Empty, "a ceiling equal to the use is tight");
            Assert.That(LocalTimeRule.SlackCeilings(sample, new Dictionary<string, int> { ["src/App/Gone.cs"] = 1 }), Is.EqualTo(new[] { "src/App/Gone.cs  uses 0, allowed 1: lower the ceiling to 0" }), "a ceiling for a file that no longer exists is slack");
        });
    }
}
