namespace Platform.Sdk.Tests;

/// <summary>
/// What Platform.Sdk makes true of every project that uses it. Each rule is proved by a sample that breaks it: the build must fail, and say which rule it was, so a rule that
/// quietly stopped applying shows up here and not in an application.
/// </summary>
[TestFixture]
public class BuildRuleTests
{
    private const string GoodCode = """
        namespace Sample;

        /// <summary>A class that follows the rules.</summary>
        public static class Greeter
        {
            /// <summary>Says hello.</summary>
            /// <param name="name">Who to greet.</param>
            /// <returns>The greeting.</returns>
            public static string Greet(string name)
            {
                return $"Hello, {name}";
            }
        }
        """;

    private static BuildResult BuildLibrary(string code, string project = "Sample", string extraProperties = "", Action<SampleBuild>? beforeBuilding = null)
    {
        using SampleBuild sample = new SampleBuild().WithProject(project, code, extraProperties);
        beforeBuilding?.Invoke(sample);
        return sample.Build(project);
    }

    [Test]
    public void AProjectThatFollowsTheRules_Builds()
    {
        BuildResult result = BuildLibrary(GoodCode);

        Assert.That(result.Succeeded, Is.True, $"a clean project builds with nothing but the SDK's rules; it printed:\n{result.Output}");
    }

    [Test]
    public void ANullDereference_FailsTheBuild()
    {
        BuildResult result = BuildLibrary(GoodCode.Replace("return $\"Hello, {name}\";", "string? missing = null;\n        return missing.Trim() + name;"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False, "nullable is on, and a warning is an error");
            Assert.That(result.Output, Does.Contain("CS8602"), "and the build says it was the null dereference");
        });
    }

    [Test]
    public void APublicTypeWithNoSummary_FailsTheBuild()
    {
        BuildResult result = BuildLibrary(GoodCode.Replace("/// <summary>A class that follows the rules.</summary>\n", ""));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False, "production code documents its public surface");
            Assert.That(result.Output, Does.Contain("CS1591"), "and the build says it was the missing summary");
        });
    }

    [Test]
    public void ATestProject_NeedsNoSummaries()
    {
        BuildResult result = BuildLibrary(GoodCode.Replace("/// <summary>A class that follows the rules.</summary>\n", ""), project: "Sample.Tests");

        Assert.That(result.Succeeded, Is.True, $"a project whose name ends in Tests is exempt from CS1591, because its method names are its documentation; it printed:\n{result.Output}");
    }

    [Test]
    public void AnUnusedUsing_FailsTheBuild()
    {
        BuildResult result = BuildLibrary("using System.Text;\n" + GoodCode);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False, "the code style is checked by the build");
            Assert.That(result.Output, Does.Contain("IDE0005"), "and the build says it was the unused using");
        });
    }

    [Test]
    public void AnIfWithNoBraces_FailsTheBuild()
    {
        BuildResult result = BuildLibrary(GoodCode.Replace("return $\"Hello, {name}\";", "if (name.Length == 0) return \"nobody\";\n        return name;"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False, "an if always has braces");
            Assert.That(result.Output, Does.Contain("IDE0011"), "and the build says it was the missing braces");
        });
    }

    [Test]
    public void ABlockNamespace_FailsTheBuild()
    {
        BuildResult result = BuildLibrary(GoodCode.Replace("namespace Sample;", "namespace Sample\n{").TrimEnd() + "\n}\n");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False, "namespaces are file-scoped");
            Assert.That(result.Output, Does.Contain("IDE0161"), "and the build says it was the block namespace");
        });
    }

    [Test]
    public void BadFormatting_FailsTheBuild()
    {
        BuildResult result = BuildLibrary(GoodCode.Replace("public static string Greet(string name)\n    {", "public static string Greet(string name) {"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False, "braces go on their own line");
            Assert.That(result.Output, Does.Contain("IDE0055"), "and the build says it was the formatting");
        });
    }

    [Test]
    public void AnApplication_CanTurnARuleOffForItself_WithAnEditorconfig()
    {
        BuildResult result = BuildLibrary(
            "using System.Text;\n" + GoodCode,
            beforeBuilding: sample => sample.Write(".editorconfig", "root = true\n\n[*.cs]\ndotnet_diagnostic.IDE0005.severity = none\n"));

        Assert.That(result.Succeeded, Is.True, $"the SDK's severities are defaults, and an application's own .editorconfig wins; it printed:\n{result.Output}");
    }

    [Test]
    public void TheProject_TargetsTheCurrentFramework_WithoutSayingSo()
    {
        BuildResult result = BuildLibrary(GoodCode);

        Assert.That(result.Files, Has.Member("Sample/bin/Debug/net10.0/Sample.dll"), "no TargetFramework is written in the project: it comes from the SDK");
    }

    private const string NUnitTest = """
        namespace Sample;

        [TestFixture]
        public class GreeterTests
        {
            [Test]
            public void ItSaysHello()
            {
                var greeter = Substitute.For<IComparable>();
                greeter.CompareTo(Arg.Any<object>()).Returns(0);

                Assert.That(greeter.CompareTo("anyone"), Is.EqualTo(0), "a substitute answers as told");
            }
        }
        """;

    [Test]
    public void ATestProject_GetsTheTestStack_WithoutNamingAPackage()
    {
        using SampleBuild sample = new SampleBuild().WithProject("Sample.Tests", NUnitTest);

        BuildResult result = sample.Test("Sample.Tests");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, $"a project whose name ends in Tests has NUnit, NSubstitute and their usings with no reference of its own; it printed:\n{result.Output}");
            Assert.That(result.Output, Does.Contain("Passed:     1"), "and its test ran and passed, so this is not a build that did nothing");
        });
    }

    [Test]
    public void TheTestStacksVersions_ComeFromTheSdk()
    {
        using SampleBuild sample = new SampleBuild().WithProject("Sample.Tests", NUnitTest);

        BuildResult result = sample.Build("Sample.Tests");

        Assert.That(sample.Read("Sample.Tests/packages.lock.json"), Does.Match("\"NUnit\": \\{[^}]*\"resolved\": \"5\\.0\\.0\""), $"one version for every application, set in the SDK; it printed:\n{result.Output}");
    }

    [Test]
    public void AnApplicationWithCentralPackageManagement_AndNoOpinionOfItsOwn_GetsTheSdksVersions()
    {
        using SampleBuild sample = new SampleBuild()
            .WithProject("Sample.Tests", NUnitTest)
            .Write("Directory.Packages.props", "<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup></Project>");

        BuildResult result = sample.Test("Sample.Tests");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, $"an application with its own Directory.Packages.props, as mortgage-analytics has, still gets the test stack; it printed:\n{result.Output}");
            Assert.That(sample.Read("Sample.Tests/packages.lock.json"), Does.Match("\"NUnit\": \\{[^}]*\"resolved\": \"5\\.0\\.0\""), "at the SDK's versions");
        });
    }

    [Test]
    public void AnApplication_CanMoveAPackageToAnotherVersion()
    {
        using SampleBuild sample = new SampleBuild()
            .WithProject("Sample.Tests", NUnitTest)
            .Write("Directory.Packages.props", "<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup><ItemGroup><PackageVersion Include=\"NUnit\" Version=\"4.6.1\" /></ItemGroup></Project>");

        BuildResult result = sample.Build("Sample.Tests");

        Assert.That(sample.Read("Sample.Tests/packages.lock.json"), Does.Match("\"NUnit\": \\{[^}]*\"resolved\": \"4\\.6\\.1\""), $"the SDK's versions are defaults, and the application's own file wins; it printed:\n{result.Output}");
    }

    [Test]
    public void AProjectNotNamedTests_GetsNoTestPackages()
    {
        BuildResult result = BuildLibrary("""
            namespace Sample;

            /// <summary>Uses a test framework it was not given.</summary>
            public static class Misplaced
            {
                /// <summary>Asserts.</summary>
                public static void Check()
                {
                    NUnit.Framework.Assert.Pass();
                }
            }
            """);

        Assert.That(result.Succeeded, Is.False, "production code does not carry the test stack");
    }

    [Test]
    public void EveryPackage_IsPinnedInALockFile()
    {
        BuildResult result = BuildLibrary(GoodCode);

        Assert.That(result.Files, Has.Member("Sample/packages.lock.json"), "restore writes packages.lock.json, so CI can restore in locked mode");
    }
}
