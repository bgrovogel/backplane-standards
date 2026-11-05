using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Backplane.Conventions.Rules;

/// <summary>
/// The <c>var</c> standard: <c>var</c> is for a declaration whose right side already names the type (<c>var loan = new Loan(...)</c>, a cast, <c>as</c>) and for anonymous types, which have
/// no name to write. Anywhere else the type is written out, so <c>decimal interest = LoanTerms.MonthlyInterestOn(balance, rate);</c> tells the reader it is money without a trip to the
/// method. The compiler's style rules cannot say "only when apparent, and never for a built-in type" strongly enough to fail the build, so a rule reads the code instead.
/// </summary>
public static class VarRule
{
    /// <summary>Every <c>var</c> declaration in the set whose type the right side does not name.</summary>
    /// <param name="source">The files to read.</param>
    /// <returns>Where each is and the line that declares it.</returns>
    public static IReadOnlyList<string> Violations(SourceSet source) =>
        [
            .. source.Trees
                .SelectMany(tree => tree.GetRoot().DescendantNodes())
                .Select(VarUse)
                .OfType<(SyntaxToken Keyword, ExpressionSyntax? Value)>()
                .Where(use => !NamesItsType(use.Value))
                .Select(use => $"{source.Location(use.Keyword)}  {FirstLine(use.Keyword.Parent!.Parent!)}")
                .Distinct(),
        ];

    /// <summary>The <c>var</c> keyword a node declares with, and the expression that gives it its type (none for a foreach or an out variable).</summary>
    private static (SyntaxToken Keyword, ExpressionSyntax? Value)? VarUse(SyntaxNode node) => node switch
    {
        VariableDeclarationSyntax { Type: IdentifierNameSyntax { IsVar: true } type } declaration
            when declaration.Variables.Count == 1 => (type.Identifier, declaration.Variables[0].Initializer?.Value),
        ForEachStatementSyntax { Type: IdentifierNameSyntax { IsVar: true } type } => (type.Identifier, null),
        DeclarationExpressionSyntax { Type: IdentifierNameSyntax { IsVar: true } type, Designation: SingleVariableDesignationSyntax } => (type.Identifier, null),
        _ => null,
    };

    /// <summary>
    /// True when the right side states the type: a <c>new</c>, an array creation, a cast, <c>as</c>, <c>default(T)</c>, or anything containing an anonymous type
    /// (a query that selects <c>new { ... }</c> has a type no one can spell).
    /// </summary>
    private static bool NamesItsType(ExpressionSyntax? value) => value switch
    {
        null => false,
        ObjectCreationExpressionSyntax or ArrayCreationExpressionSyntax or CastExpressionSyntax or DefaultExpressionSyntax => true,
        BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AsExpression) => true,
        AwaitExpressionSyntax await => NamesItsType(await.Expression),
        _ => value.DescendantNodesAndSelf().Any(node => node is AnonymousObjectCreationExpressionSyntax),
    };

    private static string FirstLine(SyntaxNode node) => node.ToString().Split(Environment.NewLine)[0].Trim();
}
