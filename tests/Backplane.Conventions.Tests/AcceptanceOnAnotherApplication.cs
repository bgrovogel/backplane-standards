using System.Text.RegularExpressions;
using System.Xml.Linq;
using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Tests;

/// <summary>
/// The extraction is faithful if the package's rules say the same about the application they were lifted from as that application's own tests do. Point <c>BACKPLANE_ACCEPTANCE_REPO</c> at a
/// checkout of <c>mortgage-analytics</c> and every rule here is run on its source, with its settings: all of them must come back clean, and the methods past a size limit must be exactly the ones
/// its ratchet lists. Without the variable the tests are skipped, with the reason shown, because the other application lives in another repository.
/// </summary>
[TestFixture]
public partial class AcceptanceOnAnotherApplication
{
    private const string Variable = "BACKPLANE_ACCEPTANCE_REPO";

    private static readonly string[] MortgageShortNames = ["Id", "id", "Va", "To", "At", "In", "Of"];

    private string _root = "";
    private SourceSet _production = null!;
    private SourceSet _all = null!;

    [SetUp]
    public void FindTheOtherApplication()
    {
        string? root = Environment.GetEnvironmentVariable(Variable);
        Assume.That(root, Is.Not.Null.And.Not.Empty, $"set {Variable} to a checkout of mortgage-analytics to run the package's rules on it");
        _root = root!;
        string[] ignored = ["bin", "obj", "Migrations", "node_modules"];
        _production = SourceSet.OfFolders(_root, ["src"], ignored);
        _all = SourceSet.Together(_production, SourceSet.OfFolders(_root, ["tests"], ignored));
    }

    [Test]
    public void TheFilesAreFound()
    {
        Assert.That(_all.Trees, Has.Count.GreaterThan(300), "the application's source and tests are there");
    }

    [Test]
    public void VarNamingFileLayoutAndSuppressions_FindNothing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(VarRule.Violations(_all), Is.Empty, "var");
            Assert.That(NamingRule.Violations(_all, MortgageShortNames), Is.Empty, "naming");
            Assert.That(FileLayoutRule.FilesWithSeveralTypes(_all), Is.Empty, "one type to a file");
            Assert.That(FileLayoutRule.FilesNotNamedAfterTheirType(_all), Is.Empty, "named after the type");
            Assert.That(FileLayoutRule.NestedClasses(_production), Is.Empty, "no nested classes");
            Assert.That(SuppressionRule.Violations(_all), Is.Empty, "suppressions");
        });
    }

    [Test]
    public void TheAssertionRules_FindNothing()
    {
        SourceSet tests = SourceSet.OfFolders(_root, ["tests"], ["bin", "obj", "Migrations", "node_modules"]);

        Assert.Multiple(() =>
        {
            Assert.That(AssertionRule.OtherThanTheConstraintModel(tests), Is.Empty, "only Assert.That and Assert.Multiple");
            Assert.That(AssertionRule.WithoutAMessage(tests), Is.Empty, "every Assert.That has a message");
            Assert.That(AssertionRule.SeveralAssertionsNotInMultiple(tests), Is.Empty, "several assertions sit in Assert.Multiple");
        });
    }

    [Test]
    public void TheClientRules_FindNothing_InTheClientTheyWereLiftedFrom()
    {
        string[] ignored = ["bin", "obj", "node_modules"];
        TextSet client = TextSet.OfFolders(_root, ["client/src", "client/e2e"], [".ts", ".html"], ignored);
        TextSet endToEnd = TextSet.OfFolders(_root, ["client/e2e"], [".ts"], ignored);

        Assert.Multiple(() =>
        {
            Assert.That(client.Files, Has.Count.GreaterThan(200), "the client's files are found");
            Assert.That(ClientSuppressionRule.Violations(client), Is.Empty, "every client suppression has a reason");
            Assert.That(endToEnd.Files, Is.Not.Empty, "the end-to-end tests are found");
            Assert.That(PlaywrightExpectRule.CallCount(endToEnd), Is.GreaterThan(100), "and its expect calls are seen");
            Assert.That(PlaywrightExpectRule.WithoutAMessage(endToEnd), Is.Empty, "every Playwright expect has a message");
        });
    }

    [Test]
    public void TheLoggingRules_FindNothing_AndSeeItsTemplates()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LoggingRule.TemplateCount(_production), Is.GreaterThan(5), "the application's LoggerMessage templates are found");
            Assert.That(LoggingRule.PersonalDataInTemplates(_production), Is.Empty, "no placeholder names personal data");
            Assert.That(LoggingRule.BuiltMessages(_production), Is.Empty, "no log call builds its message");
        });
    }

    [Test]
    public void TheClockAndDynamicRules_FindNothing_InTheApplicationTheyWereWrittenFor()
    {
        Dictionary<string, int> none = [];

        Assert.Multiple(() =>
        {
            Assert.That(LocalTimeRule.OverTheirCeiling(_production, none), Is.Empty, "no production file reads the local clock");
            Assert.That(DynamicRule.OverTheirCeiling(_production, none), Is.Empty, "no production file uses dynamic");
        });
    }

    [Test]
    public void NoNameInIt_SaysNothing()
    {
        Assert.That(VagueNamesRule.Violations(_all), Is.Empty, "the application names its types, files and folders for what they are");
    }

    [Test]
    public void TheLayers_AreRespected()
    {
        Layer[] layers =
        [
            new("MortgageAnalytics.Domain", [], MayTakePackages: [], MayOnlyUse: ["System", "MortgageAnalytics.Domain"]),
            new("MortgageAnalytics.Application", ["MortgageAnalytics.Domain"], MayTakePackages: ["FluentValidation", "FluentValidation.DependencyInjectionExtensions"],
                MustNotUse: ["Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql", "Microsoft.Data", "Amazon", "AWSSDK", "MortgageAnalytics.Infrastructure", "MortgageAnalytics.Api"]),
            new("MortgageAnalytics.Infrastructure", ["MortgageAnalytics.Application"], MustNotUse: ["MortgageAnalytics.Api"]),
            new("MortgageAnalytics.Api", ["MortgageAnalytics.Application", "MortgageAnalytics.Infrastructure"]),
        ];

        string[] problems =
        [
            .. layers.SelectMany(layer =>
            {
                XDocument project = XDocument.Load(Path.Combine(_root, "src", layer.ProjectName, $"{layer.ProjectName}.csproj"));
                return LayerRule.ProjectReferenceProblems(layer, project)
                    .Concat(LayerRule.PackageProblems(layer, project))
                    .Concat(LayerRule.UsingProblems(layer, LayerRule.SourceOf(_production, layer)));
            }),
        ];

        Assert.That(problems, Is.Empty, "dependencies point inward");
    }

    [Test]
    public void TheMethodsPastTheLimits_AreExactlyTheOnesItsRatchetLists()
    {
        string ratchet = File.ReadAllText(Path.Combine(_root, "tests", "MortgageAnalytics.Conventions.Tests", "ComplexityConventionTests.cs"));
        string[] listed = [.. Entry().Matches(ratchet).Select(match => match.Groups["key"].Value).Order(StringComparer.Ordinal)];

        string[] past = [.. MethodSize.InSource(_production).Where(size => size.Lines > ComplexityRule.DefaultMaxLines || size.Complexity > ComplexityRule.DefaultMaxComplexity).Select(size => size.Key).Order(StringComparer.Ordinal)];

        Assert.That(past, Is.EqualTo(listed), "the same measure, so the same methods");
    }

    [Test]
    public void TheMoneyRule_FindsWhatItsCeilingsAllow()
    {
        Dictionary<string, int> ceilings = new()
        {
            ["src/MortgageAnalytics.Domain/Strategies/MarketScenario.cs"] = 1,
            ["src/MortgageAnalytics.Domain/Strategies/StrategyRates.cs"] = 6,
        };

        Assert.Multiple(() =>
        {
            Assert.That(FloatingPointRule.OverTheirCeiling(_production, ["src/MortgageAnalytics.Domain/"], ceilings), Is.Empty, "no Domain file uses more than its ceiling");
            Assert.That(FloatingPointRule.SlackCeilings(_production, ["src/MortgageAnalytics.Domain/"], ceilings), Is.Empty, "and each ceiling is still needed");
        });
    }

    [GeneratedRegex(@"^\s*\[""(?<key>[^""]+)""\]\s*=\s*new\(", RegexOptions.Multiline)]
    private static partial Regex Entry();
}
