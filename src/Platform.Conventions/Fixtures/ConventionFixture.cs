using System.Collections.Concurrent;
using System.Reflection;

namespace Platform.Conventions.Fixtures;

/// <summary>
/// What every convention fixture shares: the application's source, found from the <see cref="ConventionsAttribute"/> on the assembly of the class that inherits the fixture,
/// and parsed once for the whole test run however many conventions read it.
/// </summary>
public abstract class ConventionFixture
{
    private static readonly ConcurrentDictionary<Assembly, Sources> Cache = new();

    /// <summary>The assembly that carries <c>[assembly: Conventions]</c>: the conventions project's own, unless a test says otherwise.</summary>
    protected virtual Assembly ApplicationAssembly => GetType().Assembly;

    /// <summary>The application's production code.</summary>
    protected virtual SourceSet Production => SourcesOf(ApplicationAssembly).Production;

    /// <summary>The application's test code.</summary>
    protected virtual SourceSet Tests => SourcesOf(ApplicationAssembly).Tests;

    /// <summary>Both together.</summary>
    protected virtual SourceSet All => SourcesOf(ApplicationAssembly).All;

    /// <summary>The repository's root folder.</summary>
    protected virtual string Root => SourcesOf(ApplicationAssembly).All.Root;

    /// <summary>Lines for an assertion message: one violation to a line.</summary>
    /// <param name="violations">What was found.</param>
    /// <returns>The violations, one to a line, after a line break.</returns>
    protected static string Lines(IEnumerable<string> violations) => "\n" + string.Join('\n', violations);

    private static Sources SourcesOf(Assembly assembly) => Cache.GetOrAdd(assembly, found =>
    {
        ConventionsAttribute settings = found.GetCustomAttribute<ConventionsAttribute>()
            ?? throw new InvalidOperationException($"{found.GetName().Name} has no [assembly: Conventions] attribute: add one to a file in the conventions project, so the rules can find the source.");
        SourceSet production = SourceSet.OfFolders(settings.Root, settings.ProductionFolders, settings.IgnoredFolders);
        SourceSet tests = SourceSet.OfFolders(settings.Root, settings.TestFolders, settings.IgnoredFolders);
        return new Sources(production, tests, SourceSet.Together(production, tests));
    });

    private sealed record Sources(SourceSet Production, SourceSet Tests, SourceSet All);
}
