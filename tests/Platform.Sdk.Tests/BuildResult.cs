namespace Platform.Sdk.Tests;

/// <summary>How a sample build ended: its exit code, and everything it printed.</summary>
/// <param name="ExitCode">0 for a build that succeeded.</param>
/// <param name="Output">What the build printed.</param>
/// <param name="Files">Every file in the sample when the build ended, relative to its folder with forward slashes, as the build left them.</param>
internal sealed record BuildResult(int ExitCode, string Output, IReadOnlyList<string> Files)
{
    public bool Succeeded => ExitCode == 0;
}
