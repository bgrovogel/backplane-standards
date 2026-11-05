using System.Xml.Linq;
using Platform.Conventions.Rules;

namespace Platform.Conventions.Fixtures;

/// <summary>
/// The dependency rule (<see cref="LayerRule"/>) for an application's layers. An application says what its layers are and what each may depend on; the fixture then checks every project file and every
/// <c>using</c> directive against it.
/// </summary>
[TestFixture]
public abstract class LayerConventions : ConventionFixture
{
    /// <summary>The application's layers, innermost first.</summary>
    protected abstract IReadOnlyList<Layer> Layers { get; }

    /// <summary>The folder holding the layers' projects, relative to the repository's root.</summary>
    protected virtual string SourceFolder => "src";

    /// <summary>Guards the rule against a wrong path: every layer's project file must be there.</summary>
    [Test]
    public void TheLayers_AreFound()
    {
        string[] missing = [.. Layers.Select(ProjectFile).Where(path => !File.Exists(path))];

        Assert.That(missing, Is.Empty, "the layer rule must actually be reading every layer's project file");
    }

    /// <summary>Every layer references only the projects it may.</summary>
    [Test]
    public void EveryLayer_ReferencesOnlyTheProjectsItMay()
    {
        string[] violations = [.. Layers.SelectMany(layer => LayerRule.ProjectReferenceProblems(layer, XDocument.Load(ProjectFile(layer))))];

        Assert.That(violations, Is.Empty, "dependencies point inward; an inner layer knows nothing of an outer one:" + Lines(violations));
    }

    /// <summary>The layers whose packages are limited take only those.</summary>
    [Test]
    public void TheInnerLayers_TakeOnlyThePackagesTheyMay()
    {
        string[] violations = [.. Layers.SelectMany(layer => LayerRule.PackageProblems(layer, XDocument.Load(ProjectFile(layer))))];

        Assert.That(violations, Is.Empty, "a database, web or cloud package belongs in an outer layer, behind a port:" + Lines(violations));
    }

    /// <summary>No layer uses a namespace it must not know.</summary>
    [Test]
    public void ALayer_DoesNotUseTheNamespacesItMustNotKnow()
    {
        string[] violations = [.. Layers.SelectMany(layer => LayerRule.UsingProblems(layer, LayerRule.SourceOf(Production, layer, SourceFolder)))];

        Assert.That(violations, Is.Empty, "a layer reaches a namespace it must not know; put the code in the layer that owns it, behind a port:" + Lines(violations));
    }

    private string ProjectFile(Layer layer) => Path.Combine(Root, SourceFolder, layer.ProjectName, $"{layer.ProjectName}.csproj");
}
