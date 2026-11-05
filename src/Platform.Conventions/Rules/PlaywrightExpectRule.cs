using System.Text.RegularExpressions;

namespace Platform.Conventions.Rules;

/// <summary>
/// The assertion-message standard (<see cref="AssertionRule"/>) applied to Playwright end-to-end tests: every <c>expect(actual, 'what a failure means').toX(...)</c> passes a message as its second
/// argument, so a failure explains what it means. The second argument must be text (a string or a template literal), found by reading the call's arguments properly: a comma inside a nested call, an
/// array or an object is not the one that starts the message.
/// </summary>
public static partial class PlaywrightExpectRule
{
    /// <summary>How many <c>expect(...).toX(...)</c> calls the set holds, which a scan that found none would pass without.</summary>
    /// <param name="source">The files to read.</param>
    /// <returns>The count, messages or not.</returns>
    public static int CallCount(TextSet source) => source.Files.Sum(file => Calls(file.Text).Count());

    /// <summary>Every <c>expect(...).toX(...)</c> in the set whose second argument is not a message.</summary>
    /// <param name="source">The files to read.</param>
    /// <returns>Where each is, as a clickable <c>path:line</c>, and the call as far as its matcher, first line only.</returns>
    public static IReadOnlyList<string> WithoutAMessage(TextSet source) =>
        [
            .. source.Files.SelectMany(file => Calls(file.Text)
                .Where(call => !HasAMessage(call.Arguments))
                .Select(call => $"{source.RelativePath(file.Path)}:{LineOf(file.Text, call.Start)}  {call.Text.Split('\n')[0].Trim()}")),
        ];

    private static IEnumerable<ExpectCall> Calls(string text)
    {
        foreach (Match start in ExpectOpen().Matches(text))
        {
            int open = start.Index + start.Length - 1;
            int close = ClosingBracket(text, open);
            Match matcher = close < 0 ? Match.Empty : Matcher().Match(text, close + 1);
            if (matcher.Success)
            {
                yield return new ExpectCall(start.Index, text[(open + 1)..close], text[start.Index..(matcher.Index + matcher.Length)]);
            }
        }
    }

    private static bool HasAMessage(string arguments)
    {
        List<string> parts = TopLevelParts(arguments);
        return parts.Count >= 2 && parts[1].TrimStart() is [var first, ..] && first is '\'' or '"' or '`';
    }

    /// <summary>The arguments of a call, split at the commas that are not inside a nested bracket or a string.</summary>
    private static List<string> TopLevelParts(string arguments)
    {
        List<string> parts = [];
        int depth = 0;
        int partStart = 0;
        for (int index = 0; index < arguments.Length; index++)
        {
            char character = arguments[index];
            if (IsQuote(character))
            {
                index = EndOfString(arguments, index);
            }
            else if (character is '(' or '[' or '{')
            {
                depth++;
            }
            else if (character is ')' or ']' or '}')
            {
                depth--;
            }
            else if (character == ',' && depth == 0)
            {
                parts.Add(arguments[partStart..index]);
                partStart = index + 1;
            }
        }

        parts.Add(arguments[partStart..]);
        return parts;
    }

    /// <summary>The index of the bracket that closes the one at <paramref name="open"/>, or -1 if the text ends first.</summary>
    private static int ClosingBracket(string text, int open)
    {
        int depth = 0;
        for (int index = open; index < text.Length; index++)
        {
            if (IsQuote(text[index]))
            {
                index = EndOfString(text, index);
            }
            else if (text[index] == '(')
            {
                depth++;
            }
            else if (text[index] == ')' && --depth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>The index of the quote that ends the string opened at <paramref name="open"/>; a backslash escapes the next character. The end of the text if it never closes.</summary>
    private static int EndOfString(string text, int open)
    {
        for (int index = open + 1; index < text.Length; index++)
        {
            if (text[index] == '\\')
            {
                index++;
            }
            else if (text[index] == text[open])
            {
                return index;
            }
        }

        return text.Length;
    }

    private static bool IsQuote(char character) => character is '\'' or '"' or '`';

    private static int LineOf(string text, int index) => text[..index].Count(character => character == '\n') + 1;

    [GeneratedRegex(@"\bexpect\(")]
    private static partial Regex ExpectOpen();

    /// <summary>The matcher straight after the call's closing bracket: <c>.toBe</c>, <c>.not.toBeVisible</c>.</summary>
    [GeneratedRegex(@"\G\s*\.\s*(?:not\s*\.\s*)?to[A-Z]\w*")]
    private static partial Regex Matcher();

    private sealed record ExpectCall(int Start, string Arguments, string Text);
}
