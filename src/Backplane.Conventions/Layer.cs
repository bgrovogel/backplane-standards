namespace Backplane.Conventions;

/// <summary>
/// One layer of an application's architecture, and what it may depend on: dependencies point inward, so an inner layer knows nothing of an outer one. The compiler already refuses a project
/// reference that is missing; the rule catches the one that gets added, and the <c>using</c> that reaches past a layer through a package a layer already has.
/// </summary>
/// <param name="ProjectName">The layer's project, which is also its folder under the source folder: <c>MyApp.Domain</c>.</param>
/// <param name="MayReference">The projects it may reference by name (<c>MyApp.Domain</c>); none for the innermost layer.</param>
/// <param name="MayTakePackages">The packages it may have, or null when it is an adapter or host and packages are not limited.</param>
/// <param name="MustNotUse">Namespaces it must not use (<c>Microsoft.EntityFrameworkCore</c>, <c>MyApp.Api</c>).</param>
/// <param name="MayOnlyUse">Namespaces it may use, if it is limited to its own and the base library (<c>System</c>, <c>MyApp.Domain</c>); null when it is not.</param>
public sealed record Layer(
    string ProjectName,
    IReadOnlyList<string> MayReference,
    IReadOnlyList<string>? MayTakePackages = null,
    IReadOnlyList<string>? MustNotUse = null,
    IReadOnlyList<string>? MayOnlyUse = null);
