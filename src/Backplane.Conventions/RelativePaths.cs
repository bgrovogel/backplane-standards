namespace Backplane.Conventions;

/// <summary>How a file's path is shown in a violation: relative to the repository's root, with forward slashes, so the same line reads the same on every machine.</summary>
internal static class RelativePaths
{
    /// <summary>A path relative to a root.</summary>
    /// <param name="root">The repository's root folder; empty when there is none, as for a sample made from text.</param>
    /// <param name="path">The file's path.</param>
    /// <returns>The relative path, or the path as given when there is no root or it is not a full path.</returns>
    public static string Under(string root, string path) =>
        root.Length > 0 && Path.IsPathRooted(path) ? Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/') : path;
}
