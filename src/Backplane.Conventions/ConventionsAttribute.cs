using System.Runtime.CompilerServices;

namespace Backplane.Conventions;

/// <summary>
/// Says where an application's source is, once, for every convention in its conventions project. Written as an assembly attribute in one file of that project:
/// <c>[assembly: Conventions]</c>, or with the folders that differ from the usual <c>src</c> and <c>tests</c>. The compiler fills in the path of the file the attribute is written in, so the
/// repository is found from that file and not from where the binaries were built (a build can write its output anywhere).
/// </summary>
/// <param name="declaredIn">Filled in by the compiler: the path of the file that declares the attribute.</param>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ConventionsAttribute([CallerFilePath] string declaredIn = "") : Attribute
{
    /// <summary>The folder of the file that declares the attribute.</summary>
    public string DeclaredIn { get; } = Path.GetDirectoryName(declaredIn) ?? "";

    /// <summary>From the declaring file's folder up to the repository's root, as a relative path: <c>../..</c> for a file in <c>tests/App.Conventions.Tests</c>.</summary>
    public string RootFromThisFile { get; init; } = "../..";

    /// <summary>The repository's production code folders, relative to its root.</summary>
    public string[] ProductionFolders { get; init; } = ["src"];

    /// <summary>The repository's test code folders, relative to its root.</summary>
    public string[] TestFolders { get; init; } = ["tests"];

    /// <summary>Folders whose files are build output or generated, and are never read, by name wherever they are.</summary>
    public string[] IgnoredFolders { get; init; } = ["bin", "obj", "Migrations", "node_modules"];

    /// <summary>The client's code and its tests, relative to the root, which the TypeScript rules read.</summary>
    public string[] ClientFolders { get; init; } = ["client/src", "client/e2e"];

    /// <summary>The kinds of file in the client that the rules read, with the dot.</summary>
    public string[] ClientExtensions { get; init; } = [".ts", ".html"];

    /// <summary>The Playwright end-to-end tests, relative to the root.</summary>
    public string[] EndToEndFolders { get; init; } = ["client/e2e"];

    /// <summary>The repository's root folder.</summary>
    public string Root => Path.GetFullPath(Path.Combine(DeclaredIn, RootFromThisFile));
}
