namespace Backplane.Conventions.Rules;

/// <summary>
/// The signpost standard: every project, and every named folder that is not a project, explains itself in a <c>README.md</c> of a few lines: what lives here, what does not, who may
/// depend on it and where to read more. A stranger who opens a folder learns what it is without reading its code, and a folder with no README is a folder nobody has decided the
/// purpose of. A README that is only a heading does not count.
/// </summary>
public static class ReadmeRule
{
    /// <summary>The fewest non-blank lines under the heading that make a README more than a title.</summary>
    public const int FewestLinesBelowTheHeading = 3;

    /// <summary>Every folder with a project file in it, and the named folders that exist, as full paths.</summary>
    /// <param name="root">The repository's root folder.</param>
    /// <param name="namedFolders">Folders that are not projects but still say what they are, relative to the root (<c>docs</c>, <c>scripts</c>).</param>
    /// <returns>The folders that need a README, in order.</returns>
    public static IReadOnlyList<string> FoldersThatNeedOne(string root, IEnumerable<string> namedFolders) =>
        [
            .. Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
                .Where(project => !IsBuildOrPackageOutput(project))
                .Select(project => Path.GetDirectoryName(project)!)
                .Concat(namedFolders.Select(folder => Path.GetFullPath(Path.Combine(root, folder))).Where(Directory.Exists))
                .Distinct()
                .Order(StringComparer.Ordinal),
        ];

    /// <summary>The folders in the list that have no <c>README.md</c>.</summary>
    /// <param name="root">The repository's root folder.</param>
    /// <param name="folders">Folders that need one, as full paths.</param>
    /// <returns>Each missing folder, relative to the root.</returns>
    public static IReadOnlyList<string> Missing(string root, IEnumerable<string> folders) =>
        [.. folders.Where(folder => !File.Exists(Path.Combine(folder, "README.md"))).Select(folder => RelativePath(root, folder))];

    /// <summary>The folders whose <c>README.md</c> is only a title: it must start with a heading and have a few lines of text.</summary>
    /// <param name="root">The repository's root folder.</param>
    /// <param name="folders">Folders that need one, as full paths.</param>
    /// <returns>Each thin README's folder, relative to the root.</returns>
    public static IReadOnlyList<string> Thin(string root, IEnumerable<string> folders) =>
        [
            .. folders
                .Select(folder => Path.Combine(folder, "README.md"))
                .Where(File.Exists)
                .Where(readme => !HasAHeadingAndBody(File.ReadAllLines(readme)))
                .Select(readme => RelativePath(root, Path.GetDirectoryName(readme)!)),
        ];

    private static bool HasAHeadingAndBody(string[] lines) =>
        lines.Length > 0
        && lines[0].StartsWith("# ", StringComparison.Ordinal)
        && lines.Skip(1).Count(line => line.Trim().Length > 0) >= FewestLinesBelowTheHeading;

    private static bool IsBuildOrPackageOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => segment is "bin" or "obj" or "node_modules");

    private static string RelativePath(string root, string path) => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
}
