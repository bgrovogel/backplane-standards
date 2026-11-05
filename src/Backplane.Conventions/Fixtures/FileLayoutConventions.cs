using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Fixtures;

/// <summary>
/// The file layout standard (<see cref="FileLayoutRule"/>): one top-level type to a file, named after it, and no nested classes in production code. An application overrides
/// <see cref="GroupingFolders"/> if records may share a file somewhere other than a folder called <c>Contracts</c>.
/// </summary>
[TestFixture]
public abstract class FileLayoutConventions : ConventionFixture
{
    /// <summary>The folder names where records and enums may share a file.</summary>
    protected virtual IReadOnlyCollection<string> GroupingFolders => FileLayoutRule.DefaultGroupingFolders;

    /// <summary>Guards the rule against a wrong path.</summary>
    [Test]
    public void SourceFiles_AreFound()
    {
        Assert.That(All.Trees, Is.Not.Empty, "the file layout rule must actually be scanning the application's source");
    }

    /// <summary>A file holds one type, unless it groups records in a grouping folder.</summary>
    [Test]
    public void EveryFile_HoldsOneTopLevelType_UnlessItGroupsRecords()
    {
        IReadOnlyList<string> violations = FileLayoutRule.FilesWithSeveralTypes(All, GroupingFolders);

        Assert.That(violations, Is.Empty,
            "one top-level type per file (only records and enums in a grouping folder may share one); move each type to a file of its own:" + Lines(violations));
    }

    /// <summary>A file with one type is named after it.</summary>
    [Test]
    public void AFileWithOneType_IsNamedAfterIt()
    {
        IReadOnlyList<string> violations = FileLayoutRule.FilesNotNamedAfterTheirType(All);

        Assert.That(violations, Is.Empty, "a file is named after the type it holds (Loan.cs holds Loan); rename the file or the type:" + Lines(violations));
    }

    /// <summary>Production code nests no classes.</summary>
    [Test]
    public void ProductionCode_NestsNoClasses()
    {
        IReadOnlyList<string> violations = FileLayoutRule.NestedClasses(Production);

        Assert.That(violations, Is.Empty,
            "a class inside another class hides it in a long file; give it a file of its own (nested records for a private payload are fine):" + Lines(violations));
    }
}
