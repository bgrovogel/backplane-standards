using Platform.Conventions.Rules;

namespace Platform.Conventions.Tests;

/// <summary>The file layout rule, proved on samples that break it and samples that do not.</summary>
[TestFixture]
public class FileLayoutRuleTests
{
    [Test]
    public void TwoTypesInOneFile_AreFound()
    {
        IReadOnlyList<string> found = FileLayoutRule.FilesWithSeveralTypes(SourceSet.OfText(("Loans/Loan.cs", "class Loan { }\nclass Payment { }\n")));

        Assert.That(found, Is.EqualTo(new[] { "Loans/Loan.cs  Loan, Payment" }), "one top-level type per file");
    }

    [Test]
    public void AGenericAndItsPlainTwin_ShareAFile()
    {
        IReadOnlyList<string> found = FileLayoutRule.FilesWithSeveralTypes(SourceSet.OfText(("Stored.cs", "class Stored { }\nclass Stored<T> { }\n")));

        Assert.That(found, Is.Empty, "Stored and Stored<T> are one name");
    }

    [Test]
    public void Records_MayShareAFile_InAGroupingFolderOnly()
    {
        const string records = "record Request(int A);\nrecord Response(int B);\nenum Kind { One }\n";

        Assert.Multiple(() =>
        {
            Assert.That(FileLayoutRule.FilesWithSeveralTypes(SourceSet.OfText(("Api/Contracts/Loan.cs", records))), Is.Empty, "in a Contracts folder, records and enums read together");
            Assert.That(FileLayoutRule.FilesWithSeveralTypes(SourceSet.OfText(("Api/Loans/Loan.cs", records))), Has.Count.EqualTo(1), "anywhere else they each get a file");
            Assert.That(FileLayoutRule.FilesWithSeveralTypes(SourceSet.OfText(("Api/Contracts/Loan.cs", "record Request(int A);\nclass Handler { }\n"))), Has.Count.EqualTo(1), "a class is never grouped");
            Assert.That(FileLayoutRule.FilesWithSeveralTypes(SourceSet.OfText(("Api/Shapes/Loan.cs", records)), ["Shapes"]), Is.Empty, "and an application can name its own grouping folder");
        });
    }

    [TestCase("Loan.cs", "class Loan { }", false, TestName = "A file named after its type")]
    [TestCase("Result.cs", "class Result<T> { }", false, TestName = "A generic type in the file of its plain name")]
    [TestCase("Loan.Part.cs", "partial class Loan { }", false, TestName = "A partial class in Name.Part.cs")]
    [TestCase("Payment.cs", "class Loan { }", true, TestName = "A file named after something else")]
    public void AFileWithOneType_IsNamedAfterIt(string file, string code, bool violation)
    {
        IReadOnlyList<string> found = FileLayoutRule.FilesNotNamedAfterTheirType(SourceSet.OfText((file, code)));

        Assert.That(found, violation ? Has.Count.EqualTo(1) : Is.Empty, "a file is named after the type it holds");
    }

    [Test]
    public void AClassInsideAClass_IsFound_ButARecordIsNot()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FileLayoutRule.NestedClasses(SourceSet.OfText("class Outer { class Inner { } }")), Has.Count.EqualTo(1), "a class hides in a long file");
            Assert.That(FileLayoutRule.NestedClasses(SourceSet.OfText("class Outer { record Payload(int A); }")), Is.Empty, "a nested record for a private payload is fine");
        });
    }
}
