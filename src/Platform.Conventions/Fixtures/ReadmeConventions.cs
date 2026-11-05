using Platform.Conventions.Rules;

namespace Platform.Conventions.Fixtures;

/// <summary>
/// The signpost standard (<see cref="ReadmeRule"/>): every project and every named folder has a README that is more than a heading. An application overrides
/// <see cref="NamedFolders"/> with the folders that are not projects but still say what they are.
/// </summary>
[TestFixture]
public abstract class ReadmeConventions : ConventionFixture
{
    /// <summary>The folders, relative to the repository's root, that need a README though they hold no project (<c>docs</c>, <c>scripts</c>).</summary>
    protected virtual IReadOnlyCollection<string> NamedFolders => [];

    /// <summary>Guards the rule against a wrong path: some folder must be found.</summary>
    [Test]
    public void ProjectsAndFolders_AreFound()
    {
        Assert.That(ReadmeRule.FoldersThatNeedOne(Root, NamedFolders), Is.Not.Empty, "the README rule must actually be finding the application's projects");
    }

    /// <summary>Every project and named folder has a README.</summary>
    [Test]
    public void EveryProjectAndNamedFolder_HasAReadme()
    {
        IReadOnlyList<string> missing = ReadmeRule.Missing(Root, ReadmeRule.FoldersThatNeedOne(Root, NamedFolders));

        Assert.That(missing, Is.Empty, "add a README.md of a few lines to each folder: what lives here, what does not, who may depend on it, where to read more:" + Lines(missing));
    }

    /// <summary>No README is only a title.</summary>
    [Test]
    public void EveryReadme_IsMoreThanAHeading()
    {
        IReadOnlyList<string> thin = ReadmeRule.Thin(Root, ReadmeRule.FoldersThatNeedOne(Root, NamedFolders));

        Assert.That(thin, Is.Empty,
            $"a README starts with a # heading and has at least {ReadmeRule.FewestLinesBelowTheHeading} more lines of text, so it says what the folder is:" + Lines(thin));
    }
}
