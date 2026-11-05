namespace Platform.Conventions.Tests;

/// <summary>The set of files a rule reads: which files from a folder, where its root is, and how a path is shown.</summary>
[TestFixture]
public class SourceSetTests
{
    private string _root = "";

    [SetUp]
    public void MakeARepository()
    {
        _root = Path.Combine(Path.GetTempPath(), "source-set-" + Guid.NewGuid().ToString("N"));
        foreach (string file in new[] { "src/A.cs", "src/notes.txt", "src/bin/B.cs", "src/deep/obj/C.cs", "src/Deep/E.cs", "other/F.cs" })
        {
            string full = Path.Combine(_root, file);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, "class X { }");
        }
    }

    [TearDown]
    public void RemoveTheRepository()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void AFolder_GivesItsCSharpFiles_AndNothingUnderAnIgnoredFolderOrOfAnotherKind()
    {
        SourceSet set = SourceSet.OfFolders(_root, ["src"], ["bin", "obj"]);

        string[] names = [.. set.Trees.Select(tree => Path.GetFileName(tree.FilePath)).Order()];

        Assert.That(names, Is.EqualTo(new[] { "A.cs", "E.cs" }), "C# files only; bin and obj are build output wherever they sit");
    }

    [Test]
    public void ASetOfFolders_ReadsEachFolderItIsGiven_AndSkipsOneThatIsNotThere()
    {
        SourceSet set = SourceSet.OfFolders(_root, ["other", "missing"], ["bin"]);

        Assert.That(set.Trees.Select(tree => Path.GetFileName(tree.FilePath)), Is.EqualTo(new[] { "F.cs" }), "a folder that does not exist is not an error");
    }

    [Test]
    public void ASetMadeFromText_HasNoRoot_AndASingleFileIsCalledSampleDotCs()
    {
        SourceSet single = SourceSet.OfText("class A { }");

        Assert.Multiple(() =>
        {
            Assert.That(single.Root, Is.Empty, "text has no folder to be relative to");
            Assert.That(single.Trees.Single().FilePath, Is.EqualTo("Sample.cs"), "a lone sample gets a fixed name");
        });
    }

    [Test]
    public void SetsTogether_TakeTheFirstOnesRoot_AndNoSetsHaveNone()
    {
        SourceSet first = SourceSet.OfFolders(_root, ["src"], []);
        SourceSet second = SourceSet.OfFolders(_root, ["other"], []);

        SourceSet both = SourceSet.Together(first, second);

        Assert.Multiple(() =>
        {
            Assert.That(both.Root, Is.EqualTo(_root), "the first set's root is the joined set's root");
            Assert.That(both.Trees, Has.Count.EqualTo(first.Trees.Count + second.Trees.Count), "and every file of both is in it");
            Assert.That(SourceSet.Together().Root, Is.Empty, "no sets, no root");
        });
    }

    [Test]
    public void APath_IsShownRelativeToTheRoot_OnlyWhenThereIsARootAndThePathIsRooted()
    {
        SourceSet rooted = SourceSet.OfFolders(_root, ["src"], []);
        string absolute = Path.GetFullPath("elsewhere.cs");

        Assert.Multiple(() =>
        {
            Assert.That(rooted.RelativePath(Path.Combine(_root, "src", "A.cs")), Is.EqualTo("src/A.cs"), "under the root, shown with forward slashes");
            Assert.That(rooted.RelativePath("already/relative.cs"), Is.EqualTo("already/relative.cs"), "a relative path is left alone");
            Assert.That(SourceSet.OfText("class A { }").RelativePath(absolute), Is.EqualTo(absolute), "with no root there is nothing to be relative to");
        });
    }
}
