using Backplane.Conventions.Fixtures;

namespace Backplane.Conventions.Tests;

/// <summary>The layer standard, on this repository's code: the conventions library depends on nothing of ours and takes only Roslyn and NUnit.</summary>
public class Dependencies : LayerConventions
{
    /// <inheritdoc />
    protected override IReadOnlyList<Layer> Layers { get; } =
    [
        new Layer("Backplane.Conventions", [], MayTakePackages: ["Microsoft.CodeAnalysis.CSharp", "NUnit"]),
    ];
}
