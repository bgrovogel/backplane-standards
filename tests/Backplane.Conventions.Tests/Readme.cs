using Backplane.Conventions.Fixtures;

namespace Backplane.Conventions.Tests;

/// <summary>The signpost standard, on this repository's code.</summary>
public class Readme : ReadmeConventions
{
    /// <inheritdoc />
    protected override IReadOnlyCollection<string> NamedFolders => ["docs", "tools", "scripts"];
}
