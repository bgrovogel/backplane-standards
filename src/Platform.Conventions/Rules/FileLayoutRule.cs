using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Platform.Conventions.Rules;

/// <summary>
/// The file layout standard: a file holds one top-level type, and the file is named after it. Production code nests no class inside another. The one exception is a folder named
/// for grouping (<c>Contracts</c>), where the small request and response records of one feature sit together, because they are read together and a file each would scatter one wire
/// shape across a dozen tabs. Only records and enums may be grouped; a class or interface in such a file still gets its own file.
/// </summary>
public static class FileLayoutRule
{
    /// <summary>The folder where records may share a file, unless an application says otherwise.</summary>
    public static readonly IReadOnlyList<string> DefaultGroupingFolders = ["Contracts"];

    /// <summary>Files that hold more than one top-level type and are not a group of records and enums in a grouping folder.</summary>
    /// <param name="source">The files to read.</param>
    /// <param name="groupingFolders">Folder names where records and enums may share a file; <see cref="DefaultGroupingFolders"/> when none are given.</param>
    /// <returns>The file and the types in it.</returns>
    public static IReadOnlyList<string> FilesWithSeveralTypes(SourceSet source, IReadOnlyCollection<string>? groupingFolders = null)
    {
        IReadOnlyCollection<string> grouping = groupingFolders ?? DefaultGroupingFolders;
        return
        [
            .. source.Trees
                .Select(tree => (Tree: tree, Types: TopLevelTypes(tree)))
                .Where(file => DistinctNames(file.Types).Count > 1 && !IsGroupOfRecords(file.Tree.FilePath, file.Types, grouping))
                .Select(file => $"{source.RelativePath(file.Tree.FilePath)}  {string.Join(", ", file.Types.Select(type => type.Identifier.Text))}"),
        ];
    }

    /// <summary>Files that hold one type and are not named after it.</summary>
    /// <param name="source">The files to read.</param>
    /// <returns>The file and the type in it.</returns>
    public static IReadOnlyList<string> FilesNotNamedAfterTheirType(SourceSet source) =>
        [
            .. source.Trees
                .Select(tree => (Tree: tree, Types: TopLevelTypes(tree)))
                .Where(file => DistinctNames(file.Types).Count == 1)
                .Where(file => !IsNamedAfter(file.Tree.FilePath, file.Types[0]))
                .Select(file => $"{source.RelativePath(file.Tree.FilePath)}  {file.Types[0].Identifier.Text}"),
        ];

    /// <summary>Classes declared inside another type.</summary>
    /// <param name="source">The files to read, usually the production code: a test fixture may nest what it needs.</param>
    /// <returns>Where each is and what it is called.</returns>
    public static IReadOnlyList<string> NestedClasses(SourceSet source) =>
        [
            .. source.Trees
                .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                .Where(declaration => declaration.Parent is TypeDeclarationSyntax)
                .Select(declaration => $"{source.Location(declaration)}  {declaration.Identifier.Text}"),
        ];

    /// <summary>The classes, records, structs, interfaces and enums declared at namespace level (or with no namespace).</summary>
    private static List<BaseTypeDeclarationSyntax> TopLevelTypes(SyntaxTree tree) =>
        [
            .. tree.GetRoot().DescendantNodes(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
                .OfType<MemberDeclarationSyntax>()
                .OfType<BaseTypeDeclarationSyntax>(),
        ];

    /// <summary><c>StoredJson</c> and <c>StoredJson&lt;T&gt;</c> are one name, so they share a file.</summary>
    private static List<string> DistinctNames(IEnumerable<BaseTypeDeclarationSyntax> types) =>
        [.. types.Select(type => type.Identifier.Text).Distinct()];

    private static bool IsGroupOfRecords(string path, IEnumerable<BaseTypeDeclarationSyntax> types, IReadOnlyCollection<string> groupingFolders) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(groupingFolders.Contains)
        && types.All(type => type is RecordDeclarationSyntax or EnumDeclarationSyntax);

    /// <summary>A generic type <c>Result&lt;T&gt;</c> lives in <c>Result.cs</c>; a partial class may be <c>Name.Part.cs</c>.</summary>
    private static bool IsNamedAfter(string path, BaseTypeDeclarationSyntax type)
    {
        string fileName = Path.GetFileNameWithoutExtension(path);
        return fileName == type.Identifier.Text || fileName.StartsWith(type.Identifier.Text + ".", StringComparison.Ordinal);
    }
}
