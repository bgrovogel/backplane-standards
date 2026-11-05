using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Platform.Conventions;

/// <summary>One method or constructor, with the two numbers that say how hard it is to read: how long it is and how many paths run through it.</summary>
/// <param name="Key">Where it is and what it is called: <c>path/File.cs Class.Method/2</c> (the number is its parameter count, which tells overloads apart).</param>
/// <param name="Location">A clickable <c>path:line</c>.</param>
/// <param name="Lines">The lines from its first to its last, braces included, without the comment above it.</param>
/// <param name="Complexity">Cyclomatic complexity: 1, plus one for every branch (if, loop, case, catch, <c>?:</c>, <c>&amp;&amp;</c>, <c>||</c>, <c>??</c>, <c>or</c> in a pattern).</param>
public sealed record MethodSize(string Key, string Location, int Lines, int Complexity)
{
    /// <summary>Every method, constructor and operator with a body in the set, measured.</summary>
    /// <param name="source">The files to read.</param>
    /// <returns>One entry for each.</returns>
    public static IReadOnlyList<MethodSize> InSource(SourceSet source) =>
        [
            .. source.Trees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<BaseMethodDeclarationSyntax>()
                .Where(member => member.Body is not null || member.ExpressionBody is not null)
                .Select(member => Measure(source, tree, member))),
        ];

    /// <summary>Cyclomatic complexity of a body: 1 plus one for each way the code can branch.</summary>
    /// <param name="body">The method body or expression body.</param>
    /// <returns>The complexity.</returns>
    public static int ComplexityOf(SyntaxNode body) => 1 + body.DescendantNodesAndSelf().Count(IsBranch);

    private static MethodSize Measure(SourceSet source, SyntaxTree tree, BaseMethodDeclarationSyntax member)
    {
        SyntaxNode body = (SyntaxNode?)member.Body ?? member.ExpressionBody!;
        int first = tree.GetLineSpan(member.Span).StartLinePosition.Line;
        int last = tree.GetLineSpan(body.Span).EndLinePosition.Line;
        string type = member.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text ?? "";
        string name = member switch
        {
            MethodDeclarationSyntax method => method.Identifier.Text,
            ConstructorDeclarationSyntax => ".ctor",
            _ => member.Kind().ToString(),
        };
        return new MethodSize($"{source.RelativePath(tree.FilePath)} {type}.{name}/{member.ParameterList.Parameters.Count}", source.Location(tree, first), last - first + 1, ComplexityOf(body));
    }

    /// <summary>Every kind of node that adds a path through the code (a switch arm does too, unless it is the discard that catches the rest).</summary>
    private static readonly HashSet<SyntaxKind> Branches =
    [
        SyntaxKind.IfStatement, SyntaxKind.ForStatement, SyntaxKind.ForEachStatement, SyntaxKind.WhileStatement, SyntaxKind.DoStatement,
        SyntaxKind.CaseSwitchLabel, SyntaxKind.CasePatternSwitchLabel, SyntaxKind.CatchClause, SyntaxKind.ConditionalExpression,
        SyntaxKind.LogicalAndExpression, SyntaxKind.LogicalOrExpression, SyntaxKind.CoalesceExpression, SyntaxKind.CoalesceAssignmentExpression,
        SyntaxKind.OrPattern,
    ];

    private static bool IsBranch(SyntaxNode node) =>
        Branches.Contains(node.Kind()) || node is SwitchExpressionArmSyntax { Pattern: not DiscardPatternSyntax };
}
