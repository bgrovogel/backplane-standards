using Platform.Conventions.Fixtures;

namespace Platform.Conventions.Tests;

/// <summary>The signpost standard, on this repository's code.</summary>
public class Readme : ReadmeConventions
{
    /// <inheritdoc />
    protected override IReadOnlyCollection<string> NamedFolders => ["docs", "tools"];
}
