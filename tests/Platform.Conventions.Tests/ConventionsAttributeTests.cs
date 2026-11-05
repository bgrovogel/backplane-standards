namespace Platform.Conventions.Tests;

/// <summary>The attribute that says where an application's source is: its defaults and how the root is found.</summary>
[TestFixture]
public class ConventionsAttributeTests
{
    [Test]
    public void TheDefaults_AreTheUsualLayout()
    {
        ConventionsAttribute attribute = new("somewhere/Conventions.cs");

        Assert.Multiple(() =>
        {
            Assert.That(attribute.RootFromThisFile, Is.EqualTo("../.."), "a file in tests/App.Conventions.Tests is two folders below the root");
            Assert.That(attribute.ProductionFolders, Is.EqualTo(new[] { "src" }), "production code is under src");
            Assert.That(attribute.TestFolders, Is.EqualTo(new[] { "tests" }), "test code is under tests");
            Assert.That(attribute.IgnoredFolders, Is.EqualTo(new[] { "bin", "obj", "Migrations", "node_modules" }), "build output and generated code are never read");
            Assert.That(attribute.ClientFolders, Is.EqualTo(new[] { "client/src", "client/e2e" }), "the client's code and its end-to-end tests are under client");
            Assert.That(attribute.ClientExtensions, Is.EqualTo(new[] { ".ts", ".html" }), "TypeScript and templates are what the client rules read");
            Assert.That(attribute.EndToEndFolders, Is.EqualTo(new[] { "client/e2e" }), "the Playwright tests are under client/e2e");
        });
    }

    [Test]
    public void TheRoot_IsFoundFromTheFolderOfTheFileThatDeclaresTheAttribute()
    {
        string declaring = Path.Combine(Path.GetTempPath(), "repo", "tests", "App.Conventions.Tests", "Conventions.cs");

        ConventionsAttribute attribute = new(declaring);

        Assert.Multiple(() =>
        {
            Assert.That(attribute.DeclaredIn, Is.EqualTo(Path.GetDirectoryName(declaring)), "the folder of the declaring file");
            Assert.That(attribute.Root, Is.EqualTo(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repo"))), "two folders up from it is the repository");
        });
    }

    [Test]
    public void ANamedRootFromThisFile_ChangesTheRoot()
    {
        ConventionsAttribute attribute = new(Path.Combine(Path.GetTempPath(), "repo", "tests", "Conventions.cs")) { RootFromThisFile = ".." };

        Assert.That(attribute.Root, Is.EqualTo(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repo"))), "an application whose conventions project is one folder down says so");
    }

    [Test]
    public void WithNoDeclaringFile_TheFolderIsEmpty()
    {
        Assert.That(new ConventionsAttribute("").DeclaredIn, Is.Empty, "an empty path has no folder");
    }
}
