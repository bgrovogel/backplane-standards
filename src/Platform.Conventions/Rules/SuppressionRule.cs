using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Platform.Conventions.Rules;

/// <summary>
/// A rule that is switched off says why. A suppression without a reason is a rule broken in silence: the next reader cannot tell a considered exception from a shortcut.
/// A <c>#pragma warning disable</c> carries a <c>//</c> reason on its own line, and <c>[SuppressMessage]</c> and <c>[ExcludeFromCodeCoverage]</c> carry a <c>Justification</c> that is
/// a sentence, not a word.
/// </summary>
public static class SuppressionRule
{
    /// <summary>The fewest characters that can be a reason: more than a rule's name or "ok".</summary>
    public const int ShortestReason = 8;

    /// <summary>Every suppression in the set that gives no reason.</summary>
    /// <param name="source">The files to read.</param>
    /// <returns>Where each is and what it is.</returns>
    public static IReadOnlyList<string> Violations(SourceSet source) =>
        [.. source.Trees.SelectMany(tree => UnexplainedPragmas(source, tree).Concat(UnjustifiedAttributes(source, tree)))];

    /// <summary>Every <c>#pragma warning disable</c> with no <c>//</c> reason on its line.</summary>
    private static IEnumerable<string> UnexplainedPragmas(SourceSet source, SyntaxTree tree) =>
        // Stryker disable once Boolean : a pragma directive is trivia of a token already, never nested in other structured trivia, so descending finds the same ones
        tree.GetRoot().DescendantTrivia(descendIntoTrivia: true)
            .Where(trivia => trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia))
            .Select(trivia => (PragmaWarningDirectiveTriviaSyntax)trivia.GetStructure()!)
            .Where(directive => directive.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
            .Where(directive => !LineOf(directive).Contains("//", StringComparison.Ordinal))
            .Select(directive => $"{source.Location(directive)}  {LineOf(directive).Trim()}");

    /// <summary><c>[SuppressMessage]</c> and <c>[ExcludeFromCodeCoverage]</c> with no <c>Justification</c> that holds a reason.</summary>
    private static IEnumerable<string> UnjustifiedAttributes(SourceSet source, SyntaxTree tree) =>
        tree.GetRoot().DescendantNodes().OfType<AttributeSyntax>()
            .Where(attribute => NameOf(attribute) is "SuppressMessage" or "ExcludeFromCodeCoverage")
            .Where(attribute => !HasJustification(attribute))
            .Select(attribute => $"{source.Location(attribute)}  [{NameOf(attribute)}] has no Justification");

    private static bool HasJustification(AttributeSyntax attribute) =>
        attribute.ArgumentList?.Arguments.Any(argument =>
            argument.NameEquals?.Name.Identifier.Text == "Justification"
            && argument.Expression is LiteralExpressionSyntax literal
            && literal.Token.ValueText.Trim().Length >= ShortestReason) == true;

    private static string NameOf(AttributeSyntax attribute)
    {
        string name = attribute.Name.ToString().Split('.').Last();
        return name.EndsWith("Attribute", StringComparison.Ordinal) ? name[..^"Attribute".Length] : name;
    }

    /// <summary>The text of the line a node starts on.</summary>
    private static string LineOf(SyntaxNode node) =>
        node.SyntaxTree.GetText().Lines[node.GetLocation().GetLineSpan().StartLinePosition.Line].ToString();
}
