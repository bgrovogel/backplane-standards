using Platform.Conventions.Rules;

namespace Platform.Conventions.Tests;

/// <summary>The README rule, proved on folders made for the purpose.</summary>
[TestFixture]
public class ReadmeRuleTests
{
    private string _root = "";

    [SetUp]
    public void MakeARepository()
    {
        _root = Directory.CreateTempSubdirectory("platform-readme-").FullName;
        Directory.CreateDirectory(Path.Combine(_root, "src", "App.Domain"));
        File.WriteAllText(Path.Combine(_root, "src", "App.Domain", "App.Domain.csproj"), "<Project />");
        Directory.CreateDirectory(Path.Combine(_root, "docs"));
    }

    [TearDown]
    public void RemoveTheRepository()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void EveryProject_AndEveryNamedFolderThatExists_NeedsAReadme()
    {
        IReadOnlyList<string> folders = ReadmeRule.FoldersThatNeedOne(_root, ["docs", "scripts"]);

        Assert.That(folders.Select(folder => Path.GetFileName(folder)), Is.EqualTo(new[] { "docs", "App.Domain" }), "the named folder that exists and the project, in path order; a named folder that does not exist is not asked for");
    }

    [Test]
    public void AProjectWithNoReadme_IsFound()
    {
        IReadOnlyList<string> missing = ReadmeRule.Missing(_root, ReadmeRule.FoldersThatNeedOne(_root, ["docs"]));

        Assert.That(missing, Is.EqualTo(new[] { "docs", "src/App.Domain" }), "each folder with no README, relative to the root");
    }

    [Test]
    public void AReadmeThatIsOnlyAHeading_IsFound_AndOneWithText_IsNot()
    {
        File.WriteAllText(Path.Combine(_root, "docs", "README.md"), "# Docs\n");
        File.WriteAllText(Path.Combine(_root, "src", "App.Domain", "README.md"), "# App.Domain\n\nWhat lives here.\n\nWhat does not.\n\nWho may depend on it.\n");

        IReadOnlyList<string> thin = ReadmeRule.Thin(_root, ReadmeRule.FoldersThatNeedOne(_root, ["docs"]));

        Assert.That(thin, Is.EqualTo(new[] { "docs" }), "a title alone does not say what the folder is");
    }

    [Test]
    public void AReadmeThatDoesNotStartWithAHeading_IsFound()
    {
        File.WriteAllText(Path.Combine(_root, "docs", "README.md"), "Some text first\n# Docs\nmore\nmore\nmore\n");

        Assert.That(ReadmeRule.Thin(_root, [Path.Combine(_root, "docs")]), Has.Count.EqualTo(1), "it starts with a heading");
    }
}
