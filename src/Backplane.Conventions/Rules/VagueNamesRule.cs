using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Backplane.Conventions.Rules;

/// <summary>
/// A name says what the thing is. <c>Utils</c>, <c>Helpers</c>, <c>Common</c> and <c>Manager</c> say nothing: they are where code goes when nobody has decided what it is. The rule reads the
/// declared types, namespaces, files and folders, splits each name into its words, and flags one with a vague word in it (<c>StringUtils</c>, <c>LoanManager</c>, a folder called <c>Helpers</c>).
/// Only whole words count, so <c>Commonwealth</c> and <c>Manifest</c> are fine. A name that is right for a reason (Identity's own <c>UserManager</c>) is allowed by an application, by name.
/// </summary>
public static partial class VagueNamesRule
{
    /// <summary>The words that say nothing, unless an application sets its own.</summary>
    public static readonly IReadOnlyList<string> DefaultVagueWords = ["utils", "util", "utility", "utilities", "helper", "helpers", "common", "manager", "managers", "misc", "stuff"];

    /// <summary>Every vague name in the set.</summary>
    /// <param name="source">The files to read.</param>
    /// <param name="allowed">Whole names that are right in this application (<c>UserManager</c>).</param>
    /// <param name="vagueWords">The words that make a name vague, compared without case; <see cref="DefaultVagueWords"/> when none are given.</param>
    /// <returns>Where each is and what kind of name it is.</returns>
    public static IReadOnlyList<string> Violations(SourceSet source, IReadOnlyCollection<string>? allowed = null, IReadOnlyCollection<string>? vagueWords = null)
    {
        IReadOnlyCollection<string> vague = vagueWords ?? DefaultVagueWords;
        IReadOnlyCollection<string> permitted = allowed ?? [];
        bool IsVague(string name) => !permitted.Contains(name) && Words(name).Any(word => vague.Contains(word, StringComparer.OrdinalIgnoreCase));

        return
        [
            .. source.Trees.SelectMany(tree => FileAndFolderNames(source, tree, IsVague).Concat(DeclaredNames(source, tree, IsVague))).Distinct(),
        ];
    }

    private static IEnumerable<string> FileAndFolderNames(SourceSet source, Microsoft.CodeAnalysis.SyntaxTree tree, Func<string, bool> isVague)
    {
        string[] segments = source.RelativePath(tree.FilePath).Split('/', '\\');
        string path = source.RelativePath(tree.FilePath);
        foreach (string folder in segments[..^1].Where(isVague))
        {
            yield return $"{path}  folder {folder}";
        }

        string file = Path.GetFileNameWithoutExtension(segments[^1]);
        if (isVague(file))
        {
            yield return $"{path}  file {file}";
        }
    }

    private static IEnumerable<string> DeclaredNames(SourceSet source, Microsoft.CodeAnalysis.SyntaxTree tree, Func<string, bool> isVague)
    {
        foreach (BaseTypeDeclarationSyntax type in tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Where(type => isVague(type.Identifier.Text)))
        {
            yield return $"{source.Location(type)}  type {type.Identifier.Text}";
        }

        foreach (BaseNamespaceDeclarationSyntax space in tree.GetRoot().DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
        {
            foreach (string part in space.Name.ToString().Split('.').Where(isVague))
            {
                yield return $"{source.Location(space)}  namespace {part}";
            }
        }
    }

    /// <summary>The words of a name: <c>StringUtils</c> is <c>String</c> and <c>Utils</c>; a leading <c>I</c> before another capital (<c>IHelper</c>) is not a word.</summary>
    private static IEnumerable<string> Words(string name) =>
        PascalWords().Matches(name).Select(match => match.Value).Where((word, index) => !(index == 0 && word == "I" && name.Length > 1 && char.IsUpper(name[1])));

    [GeneratedRegex("[A-Z]+(?![a-z])|[A-Z]?[a-z]+|[0-9]+")]
    private static partial Regex PascalWords();
}
