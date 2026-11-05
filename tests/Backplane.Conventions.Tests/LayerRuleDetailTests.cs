using System.Xml.Linq;
using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Tests;

/// <summary>The layer rule's edges: a namespace is matched as a whole name, and empty references are nothing.</summary>
[TestFixture]
public class LayerRuleDetailTests
{
    private static readonly Layer Domain = new("App.Domain", [], MayTakePackages: [], MustNotUse: ["App.Api"]);

    [TestCase("using App.Api;", 1, TestName = "The namespace itself")]
    [TestCase("using App.Api.Models;", 1, TestName = "A namespace under it")]
    [TestCase("using App.Apiary;", 0, TestName = "A different namespace that starts with the same letters")]
    [TestCase("using System;", 0, TestName = "An unrelated namespace")]
    public void AForbiddenNamespace_IsMatchedAsAWholeName(string directive, int expected)
    {
        SourceSet source = SourceSet.OfText(("src/App.Domain/Thing.cs", $"{directive}\nclass Thing {{ }}"));

        Assert.That(LayerRule.UsingProblems(Domain, source), Has.Count.EqualTo(expected), "App.Api is a namespace, not a prefix of letters");
    }

    [Test]
    public void AUsingProblem_SaysWhereAndWhichNamespace()
    {
        SourceSet source = SourceSet.OfText(("src/App.Domain/Thing.cs", "using System;\nusing App.Api;\nclass Thing { }"));

        Assert.That(LayerRule.UsingProblems(Domain, source), Is.EqualTo(new[] { "src/App.Domain/Thing.cs:2  using App.Api" }), "a path and line, then the directive");
    }

    [Test]
    public void AnEmptyOrMissingInclude_IsNotAReference()
    {
        XDocument project = XDocument.Parse("<Project><ItemGroup><ProjectReference /><ProjectReference Include=\"\" /><PackageReference Include=\"\" /><PackageReference /></ItemGroup></Project>");

        Assert.Multiple(() =>
        {
            Assert.That(LayerRule.ProjectReferenceProblems(Domain, project), Is.Empty, "a project reference with nothing to point at is no reference");
            Assert.That(LayerRule.PackageProblems(Domain, project), Is.Empty, "and neither is a package reference");
        });
    }

    [Test]
    public void AReferenceToAnotherProject_IsReportedByItsName()
    {
        XDocument project = XDocument.Parse("<Project><ItemGroup><ProjectReference Include=\"..\\App.Api\\App.Api.csproj\" /></ItemGroup></Project>");

        Assert.That(LayerRule.ProjectReferenceProblems(Domain, project), Is.EqualTo(new[] { "App.Domain references App.Api" }), "the name without path or extension");
    }
}
