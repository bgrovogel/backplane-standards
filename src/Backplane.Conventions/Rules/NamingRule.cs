using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Backplane.Conventions.Rules;

/// <summary>
/// The naming standard: a name says what the thing is. A local, a parameter (a lambda's included), a method, a field, a property or an enum member is at least
/// <see cref="MinimumLength"/> letters, <c>payment =&gt; payment.Amount</c>, never <c>p =&gt; p.Amount</c>. The compiler's naming rules cannot express a minimum length, so a rule reads the code.
/// </summary>
public static class NamingRule
{
    /// <summary>The fewest letters a name may have.</summary>
    public const int MinimumLength = 3;

    /// <summary>The short names that are the right name anywhere: <c>Id</c> is an identifier.</summary>
    public static readonly IReadOnlyList<string> DefaultAllowedShortNames = ["Id", "id"];

    /// <summary>Every declared name in the set that is too short.</summary>
    /// <param name="source">The files to read.</param>
    /// <param name="allowedShortNames">Short names that are right in this application (a loan type <c>Va</c>, a wire field <c>To</c>); <see cref="DefaultAllowedShortNames"/> when none are given.</param>
    /// <returns>Where each is and what it is called.</returns>
    public static IReadOnlyList<string> Violations(SourceSet source, IReadOnlyCollection<string>? allowedShortNames = null)
    {
        IReadOnlyCollection<string> allowed = allowedShortNames ?? DefaultAllowedShortNames;
        return
        [
            .. source.Trees
                .SelectMany(tree => tree.GetRoot().DescendantNodes())
                .SelectMany(DeclaredNames)
                .Where(name => IsTooShort(name.Text, allowed))
                .Select(name => $"{source.Location(name)}  {name.Text}")
                .Distinct(),
        ];
    }

    /// <summary>The names a node declares: locals, parameters (lambdas included), methods, fields, properties and so on.</summary>
    private static IEnumerable<SyntaxToken> DeclaredNames(SyntaxNode node) => node switch
    {
        VariableDeclaratorSyntax variable => [variable.Identifier],
        ParameterSyntax parameter => [parameter.Identifier],
        MethodDeclarationSyntax method => [method.Identifier],
        LocalFunctionStatementSyntax function => [function.Identifier],
        PropertyDeclarationSyntax property => [property.Identifier],
        ForEachStatementSyntax loop => [loop.Identifier],
        SingleVariableDesignationSyntax designation => [designation.Identifier],
        CatchDeclarationSyntax catchDeclaration => [catchDeclaration.Identifier],
        EnumMemberDeclarationSyntax member => [member.Identifier],
        _ => [],
    };

    /// <summary>A discard (<c>_</c>, <c>__</c>) says the name is unused, so it needs no name; an empty name is no name at all.</summary>
    private static bool IsTooShort(string name, IReadOnlyCollection<string> allowed) =>
        name.Length is > 0 and < MinimumLength && name.Any(letter => letter != '_') && !allowed.Contains(name);
}
