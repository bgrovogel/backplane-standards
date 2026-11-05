using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Platform.Conventions.Rules;

/// <summary>
/// How an application logs, held by two rules. A log line is a message template with named placeholders (<c>{Method} {Path}</c>), never a string built by interpolation or concatenation, so each
/// value is a field that can be searched and nothing is formatted just in case. And a placeholder never names personal data or a secret: log an id, not an email address, a name, a password,
/// a token or a one-time code. Both read source-generated <c>[LoggerMessage]</c> templates and direct <c>logger.LogX(...)</c> calls.
/// </summary>
public static class LoggingRule
{
    /// <summary>Placeholder names that would put a person's data or a credential into a log.</summary>
    public static readonly IReadOnlyList<string> DefaultBannedPlaceholders =
    [
        "Email", "EmailAddress", "To", "From", "Recipient", "Phone", "PhoneNumber", "Name", "FirstName", "LastName", "FullName",
        "Address", "Password", "Token", "Secret", "Credential", "Code", "Query", "QueryString", "Body", "Income", "Debts",
    ];

    private static readonly string[] LogMethods = ["Log", "LogTrace", "LogDebug", "LogInformation", "LogWarning", "LogError", "LogCritical"];

    /// <summary>How many <c>[LoggerMessage]</c> templates the set holds, which a scan that found none would pass without.</summary>
    /// <param name="source">The files to read.</param>
    /// <returns>The count.</returns>
    public static int TemplateCount(SourceSet source) => Templates(source).Count();

    /// <summary>Templates with a placeholder that names personal data or a secret.</summary>
    /// <param name="source">The files to read.</param>
    /// <param name="banned">The banned names, compared without case; <see cref="DefaultBannedPlaceholders"/> when none are given.</param>
    /// <returns>Where each is, the placeholder and the template.</returns>
    public static IReadOnlyList<string> PersonalDataInTemplates(SourceSet source, IReadOnlyCollection<string>? banned = null)
    {
        IReadOnlyCollection<string> names = banned ?? DefaultBannedPlaceholders;
        return
        [
            .. Templates(source)
                .SelectMany(template => Placeholders(template.Text)
                    .Where(placeholder => names.Contains(placeholder, StringComparer.OrdinalIgnoreCase))
                    .Select(placeholder => $"{template.Location}  {{{placeholder}}} in \"{Shorten(template.Text)}\"")),
        ];
    }

    /// <summary>Log calls (<c>logger.LogError(...)</c>) whose message is interpolated or concatenated.</summary>
    /// <param name="source">The files to read.</param>
    /// <returns>Where each is and the call.</returns>
    public static IReadOnlyList<string> BuiltMessages(SourceSet source) =>
        [
            .. source.Trees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
                .Where(call => call.Expression is MemberAccessExpressionSyntax member && LogMethods.Contains(member.Name.Identifier.Text))
                .Where(call => call.ArgumentList.Arguments.Any(argument => IsBuiltString(argument.Expression)))
                .Select(call => $"{source.Location(call)}  {call.ToString().Split('\n')[0].Trim()}"),
        ];

    /// <summary>Every <c>[LoggerMessage]</c> message template, with where it is and its text.</summary>
    private static IEnumerable<(string Location, string Text)> Templates(SourceSet source) =>
        source.Trees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<AttributeSyntax>())
            .Where(attribute => attribute.Name.ToString().Split('.').Last() is "LoggerMessage" or "LoggerMessageAttribute")
            .Select(attribute => (Attribute: attribute, Text: MessageOf(attribute)))
            .Where(found => found.Text is not null)
            .Select(found => (source.Location(found.Attribute), found.Text!));

    /// <summary>The message text: the <c>Message =</c> argument, or the third positional argument of the older form.</summary>
    private static string? MessageOf(AttributeSyntax attribute)
    {
        SeparatedSyntaxList<AttributeArgumentSyntax> arguments = attribute.ArgumentList?.Arguments ?? default;
        AttributeArgumentSyntax? named = arguments.FirstOrDefault(argument => argument.NameEquals?.Name.Identifier.Text == "Message");
        AttributeArgumentSyntax? chosen = named ?? arguments.Where(argument => argument.NameEquals is null).Skip(2).FirstOrDefault();
        return chosen?.Expression is LiteralExpressionSyntax literal ? literal.Token.ValueText : null;
    }

    /// <summary>The names in a template's <c>{Name}</c> and <c>{Name:format}</c> placeholders.</summary>
    private static IEnumerable<string> Placeholders(string template)
    {
        int open = template.IndexOf('{');
        while (open >= 0)
        {
            int close = template.IndexOf('}', open);
            if (close < 0)
            {
                yield break;
            }

            yield return template[(open + 1)..close].Split(':', ',')[0].TrimStart('@', '$');
            open = template.IndexOf('{', close);
        }
    }

    private static bool IsBuiltString(ExpressionSyntax expression) =>
        expression is InterpolatedStringExpressionSyntax
        || (expression is BinaryExpressionSyntax binary
            && binary.IsKind(SyntaxKind.AddExpression)
            && binary.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>().Any(literal => literal.IsKind(SyntaxKind.StringLiteralExpression)));

    private static string Shorten(string text) => text.Length <= 60 ? text : text[..57] + "...";
}
