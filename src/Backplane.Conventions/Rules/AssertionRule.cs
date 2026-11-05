using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Backplane.Conventions.Rules;

/// <summary>
/// The assertion standards, for every test file: only the constraint model (<c>Assert.That</c> and <c>Assert.Multiple</c>); every <c>Assert.That</c> has a failure message; and a method
/// with more than one assertion wraps them all in <c>Assert.Multiple</c>, so a failure reports every broken expectation and not just the first.
/// </summary>
public static class AssertionRule
{
    private static readonly string[] AllowedAssertMembers = ["That", "Multiple"];

    /// <summary>Calls of an <c>Assert</c> member other than <c>That</c> and <c>Multiple</c> (<c>Assert.AreEqual</c>, <c>Assert.True</c>).</summary>
    /// <param name="tests">The test files.</param>
    /// <returns>Where each is and what is called.</returns>
    public static IReadOnlyList<string> OtherThanTheConstraintModel(SourceSet tests) =>
        [
            .. AssertInvocations(tests)
                .Where(invocation => !AllowedAssertMembers.Contains(MemberName(invocation)))
                .Select(invocation => $"{tests.Location(invocation)}  Assert.{MemberName(invocation)}"),
        ];

    /// <summary>Every <c>Assert.That</c> with no third argument, which is the failure message.</summary>
    /// <param name="tests">The test files.</param>
    /// <returns>Where each is.</returns>
    public static IReadOnlyList<string> WithoutAMessage(SourceSet tests) =>
        [
            .. AssertInvocations(tests)
                .Where(invocation => MemberName(invocation) == "That" && invocation.ArgumentList.Arguments.Count < 3)
                .Select(tests.Location),
        ];

    /// <summary>Methods with several <c>Assert.That</c> calls, not all inside <c>Assert.Multiple</c>.</summary>
    /// <param name="tests">The test files.</param>
    /// <returns>Where each method is and what it is called.</returns>
    public static IReadOnlyList<string> SeveralAssertionsNotInMultiple(SourceSet tests) =>
        [
            .. tests.Trees
                .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
                .Where(method => AssertThatsIn(method).Count > 1 && AssertThatsIn(method).Any(assertion => !IsInsideAssertMultiple(assertion)))
                .Select(method => $"{tests.Location(method)}  {NameOf(method)}"),
        ];

    private static IEnumerable<InvocationExpressionSyntax> AssertInvocations(SourceSet tests) =>
        tests.Trees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "Assert" } });

    private static string MemberName(InvocationExpressionSyntax invocation) => ((MemberAccessExpressionSyntax)invocation.Expression).Name.Identifier.Text;

    private static List<InvocationExpressionSyntax> AssertThatsIn(SyntaxNode method) =>
        [
            .. method.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(assertion => assertion.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "Assert" }, Name.Identifier.Text: "That" }),
        ];

    private static bool IsInsideAssertMultiple(SyntaxNode node) =>
        node.Ancestors()
            .OfType<InvocationExpressionSyntax>()
            .Any(assertion => assertion.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "Assert" }, Name.Identifier.Text: "Multiple" });

    private static string NameOf(BaseMethodDeclarationSyntax method) => method is MethodDeclarationSyntax named ? named.Identifier.Text : method.Kind().ToString();
}
