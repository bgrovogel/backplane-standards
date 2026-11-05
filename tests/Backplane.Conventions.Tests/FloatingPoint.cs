using Backplane.Conventions.Fixtures;

namespace Backplane.Conventions.Tests;

/// <summary>The money standard, on this repository's code: it holds no money, so its folders are none that exist.</summary>
public class FloatingPoint : FloatingPointConventions
{
    /// <inheritdoc />
    protected override IReadOnlyCollection<string> MoneyFolders => ["src/Backplane.Conventions/"];
}
