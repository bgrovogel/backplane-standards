using Platform.Conventions.Rules;

namespace Platform.Conventions.Tests;

/// <summary>The README rule's edges: which project folders are build output, and how thin is too thin.</summary>
[TestFixture]
public class ReadmeRuleDetailTests
{
    private string _root = "";

    [SetUp]
    public void MakeAnEmptyRepository()
    {
        _root = Path.Combine(Path.GetTempPath(), "readme-rule-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void RemoveTheRepository()
    {
        Directory.Delete(_root, recursive: true);
    }

    [TestCase("bin")]
    [TestCase("obj")]
    [TestCase("node_modules")]
    public void AProjectFileInBuildOrPackageOutput_NeedsNoReadme(string output)
    {
        Write($"src/App/{output}/Deep/Other.csproj", "<Project />");
        Write("src/App/App.csproj", "<Project />");

        string[] folders = [.. ReadmeRule.FoldersThatNeedOne(_root, []).Select(folder => Path.GetRelativePath(_root, folder).Replace('\\', '/'))];

        Assert.That(folders, Is.EqualTo(new[] { "src/App" }), $"a project inside {output} is not ours");
    }

    [Test]
    public void ARealFolderWhoseNameContainsAnOutputName_IsStillAProject()
    {
        Write("src/binary/Binary.csproj", "<Project />");

        string[] folders = [.. ReadmeRule.FoldersThatNeedOne(_root, []).Select(folder => Path.GetRelativePath(_root, folder).Replace('\\', '/'))];

        Assert.That(folders, Is.EqualTo(new[] { "src/binary" }), "only a folder named exactly bin, obj or node_modules is output");
    }

    [Test]
    public void AnEmptyReadme_IsThin_AndSoIsOneWithoutAHeading()
    {
        Write("src/Empty/README.md", "");
        Write("src/Plain/README.md", "No heading\nline two\nline three\nline four\n");

        string[] folders = [Path.Combine(_root, "src", "Empty"), Path.Combine(_root, "src", "Plain")];

        Assert.That(ReadmeRule.Thin(_root, folders), Is.EqualTo(new[] { "src/Empty", "src/Plain" }), "no text, or text that does not start with a heading, is not a README");
    }

    [TestCase("# T\none\ntwo\n", true, TestName = "Two lines below the heading")]
    [TestCase("# T\none\n\n\ntwo\n\nthree\n", false, TestName = "Three lines with blank ones between")]
    [TestCase("# T\none\ntwo\n   \n\t\n", true, TestName = "Two lines and blank ones")]
    public void ABody_NeedsThreeLinesThatAreNotBlank(string text, bool thin)
    {
        Write("src/App/README.md", text);

        Assert.That(ReadmeRule.Thin(_root, [Path.Combine(_root, "src", "App")]), Has.Count.EqualTo(thin ? 1 : 0), "blank lines do not count as text");
    }

    private void Write(string relative, string text)
    {
        string full = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }
}
