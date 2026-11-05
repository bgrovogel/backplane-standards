namespace Backplane.Conventions.Tests;

/// <summary>The set of text files a TypeScript rule reads: which files from a folder, and how a path is shown.</summary>
[TestFixture]
public class TextSetTests
{
    private string _root = "";

    [SetUp]
    public void MakeARepository()
    {
        _root = Path.Combine(Path.GetTempPath(), "text-set-" + Guid.NewGuid().ToString("N"));
        foreach (string file in new[] { "client/src/a.ts", "client/src/page.html", "client/src/style.scss", "client/src/node_modules/b.ts", "client/e2e/c.spec.ts", "other/d.ts" })
        {
            string full = Path.Combine(_root, file);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, "text of " + file);
        }
    }

    [TearDown]
    public void RemoveTheRepository()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void AFolder_GivesFilesOfTheWantedKindsOnly_AndNothingUnderAnIgnoredFolder()
    {
        TextSet set = TextSet.OfFolders(_root, ["client/src"], [".ts", ".html"], ["node_modules"]);

        string[] names = [.. set.Files.Select(file => Path.GetFileName(file.Path)).Order()];

        Assert.That(names, Is.EqualTo(new[] { "a.ts", "page.html" }), "the wanted extensions, and node_modules is never read");
    }

    [Test]
    public void EachFile_CarriesItsText()
    {
        TextSet set = TextSet.OfFolders(_root, ["client/e2e"], [".ts"], []);

        Assert.That(set.Files.Single().Text, Is.EqualTo("text of client/e2e/c.spec.ts"), "the rule reads the text, not only the name");
    }

    [Test]
    public void ASetOfFolders_ReadsEachFolderItIsGiven_AndSkipsOneThatIsNotThere()
    {
        TextSet set = TextSet.OfFolders(_root, ["other", "missing"], [".ts"], []);

        Assert.That(set.Files.Select(file => Path.GetFileName(file.Path)), Is.EqualTo(new[] { "d.ts" }), "a folder that does not exist is not an error");
    }

    [Test]
    public void ASetMadeFromText_HasNoRoot_AndShowsPathsAsGiven()
    {
        TextSet set = TextSet.OfText(("e2e/a.spec.ts", "code"));

        Assert.Multiple(() =>
        {
            Assert.That(set.Root, Is.Empty, "text has no folder to be relative to");
            Assert.That(set.RelativePath("e2e/a.spec.ts"), Is.EqualTo("e2e/a.spec.ts"), "so a path is shown as it was given");
            Assert.That(set.Files.Single().Text, Is.EqualTo("code"), "with its text");
        });
    }

    [Test]
    public void APath_IsShownRelativeToTheRoot_WithForwardSlashes()
    {
        TextSet set = TextSet.OfFolders(_root, ["client/src"], [".ts"], []);

        Assert.That(set.RelativePath(Path.Combine(_root, "client", "src", "a.ts")), Is.EqualTo("client/src/a.ts"), "the same form on every machine");
    }
}
