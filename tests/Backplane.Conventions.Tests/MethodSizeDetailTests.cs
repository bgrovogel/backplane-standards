namespace Backplane.Conventions.Tests;

/// <summary>How a method is named and measured: constructors, operators, and members outside any type.</summary>
[TestFixture]
public class MethodSizeDetailTests
{
    [Test]
    public void AConstructor_IsNamedCtor_AndAnOperatorByItsKind()
    {
        SourceSet source = SourceSet.OfText(("Money.cs", "class Money\n{\n    Money(int cents) { }\n\n    public static Money operator +(Money a, Money b) => a;\n}\n"));

        string[] keys = [.. MethodSize.InSource(source).Select(size => size.Key)];

        Assert.That(keys, Is.EqualTo(new[] { "Money.cs Money..ctor/1", "Money.cs Money.OperatorDeclaration/2" }), "the number is the parameter count; a constructor is .ctor");
    }

    [Test]
    public void AMethodOutsideAnyType_HasNoTypeName_AndDoesNotStopTheScan()
    {
        SourceSet source = SourceSet.OfText(("Loose.cs", "namespace Sample\n{\n    void Loose() { }\n}\n"));

        string[] keys = [.. MethodSize.InSource(source).Select(size => size.Key)];

        Assert.That(keys, Is.EqualTo(new[] { "Loose.cs .Loose/0" }), "no enclosing type, so the name starts with a dot");
    }

    [Test]
    public void ASize_SaysWhereTheMethodStartsAndHowManyLinesItTakes()
    {
        SourceSet source = SourceSet.OfText(("Loan.cs", "class Loan\n{\n    void Pay()\n    {\n        if (true) { }\n    }\n}\n"));

        MethodSize size = MethodSize.InSource(source).Single();

        Assert.That((size.Location, size.Lines, size.Complexity), Is.EqualTo(("Loan.cs:3", 4, 2)), "the first line, the lines to the closing brace, and one path more for the if");
    }
}
