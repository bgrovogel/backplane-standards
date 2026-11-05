using System.Reflection;
using NUnit.Framework.Internal;
using Platform.Conventions.Fixtures;

namespace Platform.Conventions.Tests;

/// <summary>
/// Each convention fixture, run on an application that breaks its rule and on one with no source at all. The rules are proved on samples elsewhere; this proves the fixture around a rule: it asserts what the rule
/// finds, a failure says what to do and lists each violation, and its guard fails when there is nothing to scan (so a wrong path cannot make every rule pass).
/// </summary>
[TestFixture]
public class FixtureWiringTests
{
    private const string BadCode = """
        using App.Api;
        #pragma warning disable CS1591
        class Util
        {
            class Nested { }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("a", "b")]
            void Check(int a)
            {
                var x = Compute();
                double d = 1.5;
                Assert.AreEqual(1, 2);
                Assert.That(1, Is.EqualTo(1));
                Assert.That(2, Is.EqualTo(2), "two");
                logger.LogInformation($"sent {a}");
            }
        }

        class Second { }
        """;

    private const string MailCode = """
        partial class Mail
        {
            [LoggerMessage(1, LogLevel.Information, "Sent to {Email}")]
            static partial void Sent(ILogger logger, string email);
        }
        """;

    private static readonly SourceSet Bad = SourceSet.OfText(("Loans/Util.cs", BadCode), ("Loans/Mail.cs", MailCode), ("Loans/Wrong.cs", "class Other { }"));

    private static readonly SourceSet Empty = SourceSet.Together();

    private static readonly SourceSet DomainLayer = SourceSet.OfText(("src/App.Domain/Thing.cs", "using App.Api;\nclass Thing { }"));

    private readonly List<string> _folders = [];

    [OneTimeTearDown]
    public void RemoveTheSampleFolders()
    {
        foreach (string folder in _folders)
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    public static IEnumerable<TestCaseData> ApplicationsThatBreakTheirRule()
    {
        yield return Case("Assertion", () => new AssertionOn(), Bad);
        yield return Case("Complexity", () => new ComplexityOn(), Bad, skip: nameof(ComplexityConventions.EveryCeiling_IsStillNeeded));
        yield return Case("Complexity ratchet", () => new ComplexityOn(), Bad, SlackCeilings(), nameof(ComplexityConventions.NoMethod_IsLongerOrMoreComplexThanItsCeiling));
        yield return Case("FileLayout", () => new FileLayoutOn(), Bad);
        yield return Case("FloatingPoint", () => new FloatingPointOn(), Bad);
        yield return Case("Logging", () => new LoggingOn(), Bad);
        yield return Case("Naming", () => new NamingOn(), Bad);
        yield return Case("Suppression", () => new SuppressionOn(), Bad);
        yield return Case("VagueNames", () => new VagueNamesOn(), Bad);
        yield return Case("Var", () => new VarOn(), Bad);
    }

    public static IEnumerable<TestCaseData> ApplicationsWithNoSource()
    {
        yield return Case("Assertion", () => new AssertionOn(), Empty);
        yield return Case("Complexity", () => new ComplexityOn(), Empty);
        yield return Case("FileLayout", () => new FileLayoutOn(), Empty);
        yield return Case("FloatingPoint", () => new FloatingPointOn(), Empty);
        yield return Case("Logging", () => new LoggingOn(), Empty);
        yield return Case("Naming", () => new NamingOn(), Empty);
        yield return Case("Suppression", () => new SuppressionOn(), Empty);
        yield return Case("VagueNames", () => new VagueNamesOn(), Empty);
        yield return Case("Var", () => new VarOn(), Empty);
    }

    [TestCaseSource(nameof(ApplicationsThatBreakTheirRule))]
    public void EveryRuleTest_OnAnApplicationThatBreaksIt_FailsSayingWhatToDoAndListingTheViolations(Func<ConventionFixture> make, string skip)
    {
        AssertEveryRuleTestFails(make(), skip);
    }

    [Test]
    public void TheReadmeRule_OnProjectsWithNoReadmeOrAThinOne_FailsSayingWhatToDo()
    {
        string root = NewRoot(("src/NoReadme/NoReadme.csproj", "<Project />"), ("src/Thin/Thin.csproj", "<Project />"), ("src/Thin/README.md", "# Thin\n"));

        AssertEveryRuleTestFails(Make(new ReadmeOn(), root: root), skip: "");
    }

    [Test]
    public void TheLayerRule_OnALayerThatReachesOutward_FailsSayingWhatToDo()
    {
        string root = NewRoot((
            "src/App.Domain/App.Domain.csproj",
            "<Project><ItemGroup><ProjectReference Include=\"..\\App.Api\\App.Api.csproj\" /><PackageReference Include=\"Newtonsoft.Json\" /></ItemGroup></Project>"));

        AssertEveryRuleTestFails(Make(new LayerOn(), DomainLayer, root), skip: "");
    }

    [TestCaseSource(nameof(ApplicationsWithNoSource))]
    public void EveryGuard_WhenThereIsNothingToScan_Fails(Func<ConventionFixture> make, string skip)
    {
        AssertEveryGuardFails(make());
    }

    [Test]
    public void TheReadmeAndLayerGuards_WhenThereIsNothingToScan_Fail()
    {
        string root = NewRoot();

        AssertEveryGuardFails(Make(new ReadmeOn(), root: root));
        AssertEveryGuardFails(Make(new LayerOn(), DomainLayer, root));
    }

    [TestCaseSource(nameof(ApplicationsThatBreakTheirRule))]
    public void EveryGuard_WhenThereIsSourceToScan_Passes(Func<ConventionFixture> make, string skip)
    {
        foreach (MethodInfo guard in TestsOf(make()).Where(IsAGuard))
        {
            Assert.That(FailureOf(make(), guard), Is.Null, $"{guard.Name} passes when the rule has source to read");
        }
    }

    private static TestCaseData Case(string name, Func<ConventionFixture> create, SourceSet source, IReadOnlyDictionary<string, Ceiling>? ceilings = null, string skip = "") =>
        new TestCaseData((Func<ConventionFixture>)(() => Make(create(), source, ceilings: ceilings)), skip).SetName(name);

    /// <summary>Sets what the nested fixture reads, then hands it back: the nested fixtures have no constructor arguments, because NUnit would otherwise try to run them.</summary>
    private static ConventionFixture Make(ConventionFixture fixture, SourceSet? source = null, string root = "", IReadOnlyDictionary<string, Ceiling>? ceilings = null)
    {
        Scenario.Source = source ?? Empty;
        Scenario.Root = root;
        Scenario.Ceilings = ceilings ?? new Dictionary<string, Ceiling>();
        return fixture;
    }

    private static Dictionary<string, Ceiling> SlackCeilings() => new()
    {
        ["Loans/Util.cs Util.Check/1"] = new Ceiling(999, 999),
        ["Loans/Gone.cs Gone.Method/0"] = new Ceiling(5, 5),
    };

    private static MethodInfo[] TestsOf(ConventionFixture fixture) =>
        [.. fixture.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(method => method.GetCustomAttribute<TestAttribute>() is not null)];

    private static bool IsAGuard(MethodInfo method) => method.Name.Contains("Found", StringComparison.Ordinal);

    /// <summary>
    /// Runs a fixture's test and returns why it failed, or null if it passed. NUnit records a failed assertion on the running test even when the exception is caught, so the fixture runs
    /// against a throwaway result and the real one is put back.
    /// </summary>
    private static string? FailureOf(ConventionFixture fixture, MethodInfo test)
    {
        TestExecutionContext context = TestExecutionContext.CurrentContext;
        TestResult real = context.CurrentResult;
        context.CurrentResult = context.CurrentTest.MakeTestResult();
        try
        {
            test.Invoke(fixture, null);
            return null;
        }
        catch (TargetInvocationException failure) when (failure.InnerException is AssertionException assertion)
        {
            return assertion.Message;
        }
        finally
        {
            context.CurrentResult = real;
        }
    }

    private static void AssertEveryGuardFails(ConventionFixture fixture)
    {
        foreach (MethodInfo guard in TestsOf(fixture).Where(IsAGuard))
        {
            string? message = FailureOf(fixture, guard);

            Assert.That(message, Is.Not.Null.And.Not.Empty, $"{guard.Name} must fail when there is nothing to scan");
        }
    }

    private static void AssertEveryRuleTestFails(ConventionFixture fixture, string skip)
    {
        foreach (MethodInfo test in TestsOf(fixture).Where(method => !IsAGuard(method) && method.Name != skip))
        {
            string? message = FailureOf(fixture, test);

            string[] shown = [.. (message ?? "").Split('\n').TakeWhile(line => !line.TrimStart().StartsWith("Expected:", StringComparison.Ordinal)).Select(line => line.Trim())];
            Assert.Multiple(() =>
            {
                Assert.That(message, Is.Not.Null, $"{test.Name} must fail on an application that breaks its rule");
                Assert.That(shown[0], Has.Length.GreaterThanOrEqualTo(30), $"{test.Name}: the first line says what to do about it");
                Assert.That(shown.Skip(1).ToArray(), Is.Not.Empty.And.All.Length.GreaterThanOrEqualTo(8), $"{test.Name}: each violation follows on a line of its own, saying where and what");
            });
        }
    }

    private string NewRoot(params (string Path, string Text)[] files)
    {
        string root = Path.Combine(Path.GetTempPath(), "fixture-wiring-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        _folders.Add(root);
        foreach ((string path, string text) in files)
        {
            string full = Path.Combine(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }

        return root;
    }

    private static class Scenario
    {
        public static SourceSet Source { get; set; } = Empty;

        public static string Root { get; set; } = "";

        public static IReadOnlyDictionary<string, Ceiling> Ceilings { get; set; } = new Dictionary<string, Ceiling>();
    }

    [Explicit("built and run by FixtureWiringTests, not on its own")]
    private sealed class AssertionOn : AssertionConventions
    {
        protected override SourceSet Tests => Scenario.Source;
    }

    [Explicit("built and run by FixtureWiringTests, not on its own")]
    private sealed class ComplexityOn : ComplexityConventions
    {
        protected override SourceSet Production => Scenario.Source;

        protected override int MaxLines => 2;

        protected override int MaxComplexity => 1;

        protected override IReadOnlyDictionary<string, Ceiling> Ceilings => Scenario.Ceilings;
    }

    [Explicit("built and run by FixtureWiringTests, not on its own")]
    private sealed class FileLayoutOn : FileLayoutConventions
    {
        protected override SourceSet All => Scenario.Source;

        protected override SourceSet Production => Scenario.Source;
    }

    [Explicit("built and run by FixtureWiringTests, not on its own")]
    private sealed class FloatingPointOn : FloatingPointConventions
    {
        protected override IReadOnlyCollection<string> MoneyFolders => ["Loans/"];

        protected override IReadOnlyDictionary<string, int> Ceilings => new Dictionary<string, int> { ["Loans/Other.cs"] = 3 };

        protected override SourceSet Production => Scenario.Source;
    }

    [Explicit("built and run by FixtureWiringTests, not on its own")]
    private sealed class LoggingOn : LoggingConventions
    {
        protected override SourceSet Production => Scenario.Source;
    }

    [Explicit("built and run by FixtureWiringTests, not on its own")]
    private sealed class NamingOn : NamingConventions
    {
        protected override SourceSet All => Scenario.Source;
    }

    [Explicit("built and run by FixtureWiringTests, not on its own")]
    private sealed class SuppressionOn : SuppressionConventions
    {
        protected override SourceSet All => Scenario.Source;
    }

    [Explicit("built and run by FixtureWiringTests, not on its own")]
    private sealed class VagueNamesOn : VagueNamesConventions
    {
        protected override SourceSet All => Scenario.Source;
    }

    [Explicit("built and run by FixtureWiringTests, not on its own")]
    private sealed class VarOn : VarConventions
    {
        protected override SourceSet All => Scenario.Source;
    }

    [Explicit("built and run by FixtureWiringTests, not on its own")]
    private sealed class ReadmeOn : ReadmeConventions
    {
        protected override string Root => Scenario.Root;
    }

    [Explicit("built and run by FixtureWiringTests, not on its own")]
    private sealed class LayerOn : LayerConventions
    {
        protected override string Root => Scenario.Root;

        protected override SourceSet Production => Scenario.Source;

        protected override IReadOnlyList<Layer> Layers { get; } = [new Layer("App.Domain", [], MayTakePackages: [], MustNotUse: ["App.Api"])];
    }
}
