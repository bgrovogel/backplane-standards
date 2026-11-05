namespace Platform.Conventions;

/// <summary>
/// The plain-text files a rule reads where there is no syntax tree to read: the TypeScript and templates of an application's client. Like <see cref="SourceSet"/> it is parsed (here, read) once,
/// can be made from text for a sample that breaks a rule, and shows each path relative to the repository's root.
/// </summary>
public sealed class TextSet
{
    private readonly Lazy<IReadOnlyList<TextFile>> _files;

    private TextSet(string root, Func<IReadOnlyList<TextFile>> load)
    {
        Root = root;
        _files = new Lazy<IReadOnlyList<TextFile>>(load);
    }

    /// <summary>The repository's root folder, which locations are given relative to; empty for a set made from text.</summary>
    public string Root { get; }

    /// <summary>Every file in the set, with its text.</summary>
    public IReadOnlyList<TextFile> Files => _files.Value;

    /// <summary>The files of some kinds under some folders of a repository, build output and package folders left out.</summary>
    /// <param name="root">The repository's root folder.</param>
    /// <param name="folders">The folders to read, relative to the root.</param>
    /// <param name="extensions">The kinds of file to read, with the dot (<c>.ts</c>).</param>
    /// <param name="ignoredFolders">Folder names whose files are never read, wherever they are.</param>
    /// <returns>The set, read on first use.</returns>
    public static TextSet OfFolders(string root, IEnumerable<string> folders, IEnumerable<string> extensions, IEnumerable<string> ignoredFolders)
    {
        string[] chosen = [.. folders];
        string[] wanted = [.. extensions];
        string[] ignored = [.. ignoredFolders];
        return new TextSet(root, () =>
        [
            .. chosen
                .Select(folder => Path.Combine(root, folder))
                .Where(Directory.Exists)
                .SelectMany(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                .Where(path => wanted.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => ignored.Contains(segment)))
                .Select(path => new TextFile(path, File.ReadAllText(path))),
        ]);
    }

    /// <summary>A set made of the text of files: the samples a rule is proved on.</summary>
    /// <param name="files">Each file's path (relative, with forward slashes) and its text.</param>
    /// <returns>The set.</returns>
    public static TextSet OfText(params (string Path, string Text)[] files) =>
        new(string.Empty, () => [.. files.Select(file => new TextFile(file.Path, file.Text))]);

    /// <summary>A file's path relative to the root, with forward slashes.</summary>
    /// <param name="path">The file's path.</param>
    /// <returns>The relative path, or the path as given when it is not under a root.</returns>
    public string RelativePath(string path) => RelativePaths.Under(Root, path);
}
