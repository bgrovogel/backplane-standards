using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Backplane.Conventions.Rules;

/// <summary>
/// The dependency rule, checked from the outside (see <see cref="Layer"/>). Each check is a function over a project file or the source of one layer, so it is shown to flag a bad sample before it is
/// trusted on the real source. It reads project files and <c>using</c> directives, so it needs no extra package and no loaded assembly.
/// </summary>
public static class LayerRule
{
    /// <summary>References from the layer's project file to projects it may not know.</summary>
    /// <param name="layer">The layer.</param>
    /// <param name="project">Its project file.</param>
    /// <returns>Each reference that points the wrong way.</returns>
    public static IReadOnlyList<string> ProjectReferenceProblems(Layer layer, XDocument project) =>
        [
            .. References(project, "ProjectReference")
                .Where(reference => !layer.MayReference.Contains(reference))
                .Select(reference => $"{layer.ProjectName} references {reference}"),
        ];

    /// <summary>Packages in the layer's project file that it may not take; none are flagged for a layer whose packages are not limited.</summary>
    /// <param name="layer">The layer.</param>
    /// <param name="project">Its project file.</param>
    /// <returns>Each package it should not have.</returns>
    public static IReadOnlyList<string> PackageProblems(Layer layer, XDocument project) =>
        layer.MayTakePackages is null
            ? []
            : [.. References(project, "PackageReference").Where(package => !layer.MayTakePackages.Contains(package)).Select(package => $"{layer.ProjectName} takes the package {package}")];

    /// <summary>The <c>using</c> directives in the layer's source that it may not have.</summary>
    /// <param name="layer">The layer.</param>
    /// <param name="source">The source files of the layer (use <see cref="SourceOf"/>).</param>
    /// <returns>Where each is and the namespace it names.</returns>
    public static IReadOnlyList<string> UsingProblems(Layer layer, SourceSet source) =>
        [
            .. source.Trees
                .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>())
                .Select(directive => (Directive: directive, Name: directive.Name?.ToString() ?? string.Empty))
                .Where(use => layer.MayOnlyUse is not null ? !StartsWithAny(use.Name, layer.MayOnlyUse) : StartsWithAny(use.Name, layer.MustNotUse ?? []))
                .Select(use => $"{source.Location(use.Directive)}  using {use.Name}"),
        ];

    /// <summary>The files of one layer: those under <c>sourceFolder/ProjectName/</c>.</summary>
    /// <param name="all">Every file in the application.</param>
    /// <param name="layer">The layer.</param>
    /// <param name="sourceFolder">The folder holding the projects, relative to the root (<c>src</c>).</param>
    /// <returns>A set of that layer's files.</returns>
    public static SourceSet SourceOf(SourceSet all, Layer layer, string sourceFolder = "src") =>
        SourceSet.Subset(all, tree => all.RelativePath(tree.FilePath).StartsWith($"{sourceFolder}/{layer.ProjectName}/", StringComparison.Ordinal));

    /// <summary>The names of a project file's references of one kind: a project's name without path or extension, or a package's name.</summary>
    private static List<string> References(XDocument project, string kind) =>
        [
            .. project.Descendants(kind)
                .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
                .Where(include => include.Length > 0)
                .Select(include => kind == "ProjectReference" ? Path.GetFileNameWithoutExtension(include.Replace('\\', '/')) : include),
        ];

    private static bool StartsWithAny(string name, IReadOnlyCollection<string> namespaces) =>
        namespaces.Any(space => name == space || name.StartsWith(space + ".", StringComparison.Ordinal));
}
