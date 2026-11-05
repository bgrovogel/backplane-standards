using System.Xml.Linq;
using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Tests;

/// <summary>The layer rule, proved on samples that break it and samples that do not.</summary>
[TestFixture]
public class LayerRuleTests
{
    private static readonly Layer Domain = new("App.Domain", [], MayTakePackages: [], MayOnlyUse: ["System", "App.Domain"]);
    private static readonly Layer Application = new("App.Application", ["App.Domain"], MayTakePackages: ["FluentValidation"], MustNotUse: ["Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql", "App.Api"]);
    private static readonly Layer Api = new("App.Api", ["App.Application", "App.Infrastructure"]);

    private static XDocument Project(string items) => XDocument.Parse($"<Project><ItemGroup>{items}</ItemGroup></Project>");

    [Test]
    public void AProjectReference_ToALayerItMayNotKnow_IsFlagged()
    {
        XDocument sample = Project("<ProjectReference Include=\"..\\App.Infrastructure\\App.Infrastructure.csproj\" />");

        Assert.Multiple(() =>
        {
            Assert.That(LayerRule.ProjectReferenceProblems(Application, sample), Is.EqualTo(new[] { "App.Application references App.Infrastructure" }), "the Application referencing the Infrastructure points outward");
            Assert.That(LayerRule.ProjectReferenceProblems(Api, sample), Is.Empty, "the Api may reference the Infrastructure, to register it");
        });
    }

    [Test]
    public void APackage_InTheDomain_IsFlagged_AndAnAllowedOneInTheApplicationIsNot()
    {
        XDocument sample = Project("<PackageReference Include=\"Npgsql\" /><PackageReference Include=\"FluentValidation\" />");

        Assert.Multiple(() =>
        {
            Assert.That(LayerRule.PackageProblems(Domain, sample), Has.Count.EqualTo(2), "the Domain takes no package at all");
            Assert.That(LayerRule.PackageProblems(Application, sample), Is.EqualTo(new[] { "App.Application takes the package Npgsql" }), "the Application may take FluentValidation but not Npgsql");
            Assert.That(LayerRule.PackageProblems(Api, sample), Is.Empty, "a layer whose packages are not limited is not checked");
        });
    }

    [Test]
    public void ForbiddenUsings_AreFlagged_InTheirLayer_AndOrdinaryOnesAreNot()
    {
        SourceSet sample = SourceSet.OfText("using System;\nusing Microsoft.EntityFrameworkCore;\nusing static Npgsql.NpgsqlDbType;\nusing Alias = Microsoft.AspNetCore.Http.HttpContext;\nusing FluentValidation;\nclass Sample { }");

        Assert.Multiple(() =>
        {
            Assert.That(LayerRule.UsingProblems(Application, sample), Has.Count.EqualTo(3), "a plain using, a static using and an alias are all read; System and FluentValidation are fine");
            Assert.That(LayerRule.UsingProblems(Domain, sample), Has.Count.EqualTo(4), "the Domain may use only System and its own namespaces");
            Assert.That(LayerRule.UsingProblems(Api, sample), Is.Empty, "a layer with no restrictions may use anything");
        });
    }

    [Test]
    public void ALayersFiles_AreThoseUnderItsProject()
    {
        SourceSet all = SourceSet.OfText(("src/App.Domain/Loan.cs", "class Loan { }"), ("src/App.Api/Program.cs", "class Program { }"));

        Assert.That(LayerRule.SourceOf(all, Domain).Trees.Select(tree => tree.FilePath), Is.EqualTo(new[] { "src/App.Domain/Loan.cs" }), "only the project's own folder");
    }
}
