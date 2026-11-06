using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Backplane.Conventions.Rules;

/// <summary>
/// The shape of the API belongs to the Application layer, so a contract never exposes a Domain type: if it did, renaming or reshaping that type would silently change what clients receive.
/// A public property of a type declared in the contracts folders (and a positional record's parameter, which is one) may not be, or contain, a type declared in the Domain folders. The rule
/// reads source, so it matches by name: a name that the contracts declare themselves means the contract's own type, which the application maps from the Domain's.
/// </summary>
public static class ContractRule
{
    /// <summary>The names of the types (classes, structs, records, interfaces, enums, delegates) declared in some folders.</summary>
    /// <param name="source">The files to read.</param>
    /// <param name="folders">Path prefixes, relative to the root with forward slashes, of the folders to read (<c>src/App.Domain/</c>).</param>
    /// <returns>Each name once.</returns>
    public static IReadOnlyCollection<string> TypeNames(SourceSet source, IReadOnlyCollection<string> folders) =>
        [
            .. InFolders(source, folders)
                .SelectMany(tree => tree.GetRoot().DescendantNodes())
                .Select(DeclaredName)
                .OfType<string>()
                .Distinct(),
        ];

    /// <summary>Public properties of contract types whose type is, or holds, a Domain type.</summary>
    /// <param name="source">The files to read.</param>
    /// <param name="contractFolders">Path prefixes of the folders that hold the API's contract types.</param>
    /// <param name="domainFolders">Path prefixes of the folders that hold the Domain.</param>
    /// <returns>Where each is, which property, and the Domain type it exposes.</returns>
    public static IReadOnlyList<string> DomainTypesExposed(SourceSet source, IReadOnlyCollection<string> contractFolders, IReadOnlyCollection<string> domainFolders)
    {
        HashSet<string> own = [.. TypeNames(source, contractFolders)];
        HashSet<string> domain = [.. TypeNames(source, domainFolders).Where(name => !own.Contains(name))];
        return
        [
            .. InFolders(source, contractFolders)
                .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
                .SelectMany(Exposed)
                .SelectMany(member => NamesIn(member.Type).Where(domain.Contains).Distinct().Select(name => $"{source.Location(member.Node)}  {member.Owner}.{member.Name} exposes the domain type {name}")),
        ];
    }

    private static string? DeclaredName(SyntaxNode node) => node switch
    {
        BaseTypeDeclarationSyntax declaration => declaration.Identifier.ValueText,
        DelegateDeclarationSyntax declaration => declaration.Identifier.ValueText,
        _ => null,
    };

    /// <summary>The properties a client can see: public, not static, and a positional record's parameters.</summary>
    private static IEnumerable<(SyntaxNode Node, string Owner, string Name, TypeSyntax Type)> Exposed(TypeDeclarationSyntax owner)
    {
        IEnumerable<(SyntaxNode, string, string, TypeSyntax)> properties = owner.Members.OfType<PropertyDeclarationSyntax>()
            .Where(property => IsVisible(property))
            .Select(property => ((SyntaxNode)property, owner.Identifier.ValueText, property.Identifier.ValueText, property.Type));
        IEnumerable<(SyntaxNode, string, string, TypeSyntax)> parameters = owner is RecordDeclarationSyntax record
            ? record.ParameterList?.Parameters.Where(parameter => parameter.Type is not null).Select(parameter => ((SyntaxNode)parameter, owner.Identifier.ValueText, parameter.Identifier.ValueText, parameter.Type!)) ?? []
            : [];
        return properties.Concat(parameters);
    }

    private static bool IsVisible(PropertyDeclarationSyntax property) =>
        !property.Modifiers.Any(SyntaxKind.StaticKeyword) && (property.Modifiers.Any(SyntaxKind.PublicKeyword) || property.Parent is InterfaceDeclarationSyntax);

    private static IEnumerable<string> NamesIn(TypeSyntax type) => type.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().Select(name => name.Identifier.ValueText);

    private static IEnumerable<SyntaxTree> InFolders(SourceSet source, IReadOnlyCollection<string> folders) =>
        source.Trees.Where(tree => folders.Any(folder => source.RelativePath(tree.FilePath).StartsWith(folder, StringComparison.Ordinal)));
}
