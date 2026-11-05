using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Platform.Conventions;

/// <summary>
/// The C# files a convention reads, parsed once. Rules take a set and give back the violations they find, each as a clickable <c>path:line</c> and what is wrong,
/// so a failing test says where to look. A set can be made from text, which is how a rule is proved on a sample that breaks it.
/// </summary>
public sealed class SourceSet
{
    private readonly Lazy<IReadOnlyList<SyntaxTree>> _trees;

    private SourceSet(string root, Func<IReadOnlyList<SyntaxTree>> load)
    {
        Root = root;
        _trees = new Lazy<IReadOnlyList<SyntaxTree>>(load);
    }

    /// <summary>The repository's root folder, which locations are given relative to; empty for a set made from text.</summary>
    public string Root { get; }

    /// <summary>Every parsed file in the set.</summary>
    public IReadOnlyList<SyntaxTree> Trees => _trees.Value;

    /// <summary>The C# files under some folders of a repository, build output and generated code left out.</summary>
    /// <param name="root">The repository's root folder.</param>
    /// <param name="folders">The folders to read, relative to the root.</param>
    /// <param name="ignoredFolders">Folder names whose files are never read, wherever they are.</param>
    /// <returns>The set, parsed on first use.</returns>
    public static SourceSet OfFolders(string root, IEnumerable<string> folders, IEnumerable<string> ignoredFolders)
    {
        string[] chosen = [.. folders];
        string[] ignored = [.. ignoredFolders];
        return new SourceSet(root, () =>
        [
            .. chosen
                .Select(folder => Path.Combine(root, folder))
                .Where(Directory.Exists)
                .SelectMany(folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
                .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => ignored.Contains(segment)))
                .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path)),
        ]);
    }

    /// <summary>A set made of the text of files: the samples a rule is proved on.</summary>
    /// <param name="files">Each file's path (relative, with forward slashes) and its code.</param>
    /// <returns>The set.</returns>
    public static SourceSet OfText(params (string Path, string Code)[] files) =>
        new(string.Empty, () => [.. files.Select(file => CSharpSyntaxTree.ParseText(file.Code, path: file.Path))]);

    /// <summary>A set made of one file's text, called <c>Sample.cs</c>.</summary>
    /// <param name="code">The file's code.</param>
    /// <returns>The set.</returns>
    public static SourceSet OfText(string code) => OfText(("Sample.cs", code));

    /// <summary>The sets together as one.</summary>
    /// <param name="sets">The sets to join; the first one's root is used.</param>
    /// <returns>A set of all their files.</returns>
    public static SourceSet Together(params SourceSet[] sets) =>
        new(sets.Length == 0 ? string.Empty : sets[0].Root, () => [.. sets.SelectMany(set => set.Trees)]);

    /// <summary>The files of another set that satisfy a condition.</summary>
    /// <param name="all">The set to choose from; its root is kept.</param>
    /// <param name="include">Chooses a file.</param>
    /// <returns>A set of the chosen files.</returns>
    public static SourceSet Subset(SourceSet all, Func<SyntaxTree, bool> include) =>
        new(all.Root, () => [.. all.Trees.Where(include)]);

    /// <summary>A clickable <c>path:line</c> for a node, relative to the root, with forward slashes.</summary>
    /// <param name="node">The syntax node.</param>
    /// <returns>Where it is.</returns>
    public string Location(SyntaxNode node) => Location(node.SyntaxTree, node.GetLocation().GetLineSpan().StartLinePosition.Line);

    /// <summary>A clickable <c>path:line</c> for a token.</summary>
    /// <param name="token">The syntax token.</param>
    /// <returns>Where it is.</returns>
    public string Location(SyntaxToken token) => Location(token.SyntaxTree!, token.GetLocation().GetLineSpan().StartLinePosition.Line);

    /// <summary>A clickable <c>path:line</c> for a zero-based line of a tree.</summary>
    /// <param name="tree">The parsed file.</param>
    /// <param name="zeroBasedLine">The line, counted from 0.</param>
    /// <returns>Where it is.</returns>
    public string Location(SyntaxTree tree, int zeroBasedLine) => $"{RelativePath(tree.FilePath)}:{zeroBasedLine + 1}";

    /// <summary>A file's path relative to the root, with forward slashes.</summary>
    /// <param name="path">The file's path.</param>
    /// <returns>The relative path, or the path as given when it is not under the root.</returns>
    public string RelativePath(string path) =>
        Root.Length > 0 && Path.IsPathRooted(path) ? Path.GetRelativePath(Root, path).Replace(Path.DirectorySeparatorChar, '/') : path;
}
