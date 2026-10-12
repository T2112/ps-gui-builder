// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text;
using System.Text.RegularExpressions;

namespace PsGuiBuilder.Core.Building;

internal readonly record struct PlaceholderUse(string Name, bool InsideDoubleQuotes, int Start, int Length);

internal static partial class TemplateScanner
{
    public static IReadOnlyList<PlaceholderUse> FindPlaceholders(string template, IList<string> syntaxErrors)
    {
        var found = new List<PlaceholderUse>();
        Render(template, replace: null, found, syntaxErrors);
        return found;
    }

    public static string Render(
        string template,
        Func<string, bool, string>? replace,
        IList<PlaceholderUse>? found,
        IList<string> syntaxErrors)
    {
        var builder = new StringBuilder(template.Length);
        var state = QuoteState.Normal;
        var index = 0;

        while (index < template.Length)
        {
            var character = template[index];
            if (state == QuoteState.Normal)
            {
                if (character == '"')
                {
                    state = QuoteState.Double;
                    builder.Append(character);
                    index++;
                    continue;
                }

                if (character == '\'')
                {
                    state = QuoteState.Single;
                    builder.Append(character);
                    index++;
                    continue;
                }

                if (character == '{')
                {
                    if (!TryReadPlaceholder(template, index, out var name, out var next))
                    {
                        syntaxErrors.Add("Invalid or unclosed '{' in the template.");
                        builder.Append(character);
                        index++;
                        continue;
                    }

                    found?.Add(new PlaceholderUse(name, InsideDoubleQuotes: false, index, next - index));
                    builder.Append(replace is null ? template[index..next] : replace(name, false));
                    index = next;
                    continue;
                }

                builder.Append(character);
                index++;
                continue;
            }

            if (state == QuoteState.Double)
            {
                if (character == '`')
                {
                    builder.Append(character);
                    if (index + 1 >= template.Length)
                    {
                        syntaxErrors.Add("The template ends with an incomplete escape.");
                        index++;
                        continue;
                    }

                    builder.Append(template[index + 1]);
                    index += 2;
                    continue;
                }

                if (character == '"')
                {
                    builder.Append(character);
                    if (index + 1 < template.Length && template[index + 1] == '"')
                    {
                        builder.Append('"');
                        index += 2;
                        continue;
                    }

                    state = QuoteState.Normal;
                    index++;
                    continue;
                }

                if (character == '{')
                {
                    if (!TryReadPlaceholder(template, index, out var name, out var next))
                    {
                        syntaxErrors.Add("Invalid or unclosed '{' in the template.");
                        builder.Append(character);
                        index++;
                        continue;
                    }

                    found?.Add(new PlaceholderUse(name, InsideDoubleQuotes: true, index, next - index));
                    builder.Append(replace is null ? template[index..next] : replace(name, true));
                    index = next;
                    continue;
                }

                builder.Append(character);
                index++;
                continue;
            }

            if (character == '\'' && index + 1 < template.Length && template[index + 1] == '\'')
            {
                builder.Append("''");
                index += 2;
                continue;
            }

            if (character == '\'')
            {
                state = QuoteState.Normal;
                builder.Append(character);
                index++;
                continue;
            }

            if (character == '{' && TryReadPlaceholder(template, index, out var literalName, out _))
            {
                syntaxErrors.Add(
                    $"Placeholder \"{{{literalName}}}\" is inside single quotes, so PowerShell would not expand it. Use double quotes in the template.");
            }

            builder.Append(character);
            index++;
        }

        if (state != QuoteState.Normal)
            syntaxErrors.Add("The template has an unmatched quote.");

        return builder.ToString();
    }

    private static bool TryReadPlaceholder(string template, int openIndex, out string name, out int nextIndex)
    {
        name = "";
        nextIndex = openIndex;
        var match = Placeholder().Match(template, openIndex);
        if (!match.Success || match.Index != openIndex)
            return false;

        name = match.Groups[1].Value;
        nextIndex = openIndex + match.Length;
        return true;
    }

    internal static bool IsWholeToken(string template, PlaceholderUse use)
    {
        if (use.InsideDoubleQuotes)
            return false;

        var before = use.Start > 0 ? template[use.Start - 1] : ' ';
        var afterIndex = use.Start + use.Length;
        var after = afterIndex < template.Length ? template[afterIndex] : ' ';
        return char.IsWhiteSpace(before) && char.IsWhiteSpace(after);
    }

    internal static bool TryGetRemovalRange(string template, PlaceholderUse use, out int start, out int end)
    {
        start = use.Start;
        end = use.Start + use.Length;
        if (use.InsideDoubleQuotes)
        {
            var open = use.Start - 1;
            while (open >= 0 && template[open] != '"')
            {
                if (!char.IsWhiteSpace(template[open]))
                    return false;

                open--;
            }

            if (open < 0)
                return false;

            var close = use.Start + use.Length;
            while (close < template.Length && template[close] != '"')
            {
                if (!char.IsWhiteSpace(template[close]))
                    return false;

                close++;
            }

            if (close >= template.Length || template[close] != '"')
                return false;

            start = open;
            end = close + 1;
        }
        else if (!IsWholeToken(template, use))
        {
            return false;
        }

        var cursor = start - 1;
        while (cursor >= 0 && char.IsWhiteSpace(template[cursor]))
            cursor--;

        if (cursor >= 0)
        {
            var tokenEnd = cursor + 1;
            var tokenStart = cursor;
            while (tokenStart > 0 && !char.IsWhiteSpace(template[tokenStart - 1]))
                tokenStart--;

            if (template[tokenStart] == '-' && tokenEnd - tokenStart > 1)
                start = tokenStart;
        }

        if (start > 0 && char.IsWhiteSpace(template[start - 1]))
        {
            while (start > 0 && char.IsWhiteSpace(template[start - 1]))
                start--;
        }
        else
        {
            while (end < template.Length && char.IsWhiteSpace(template[end]))
                end++;
        }

        return true;
    }

    internal static string ApplyRemovals(string template, IReadOnlyList<(int Start, int End)> ranges)
    {
        if (ranges.Count == 0)
            return template;

        var ordered = ranges.OrderBy(range => range.Start).ToArray();
        var builder = new StringBuilder(template.Length);
        var cursor = 0;
        foreach (var range in ordered)
        {
            if (range.Start < cursor || range.End < range.Start || range.End > template.Length)
                continue;

            builder.Append(template, cursor, range.Start - cursor);
            cursor = range.End;
        }

        builder.Append(template, cursor, template.Length - cursor);
        return builder.ToString().Trim();
    }

    [GeneratedRegex(@"\{([A-Za-z_][A-Za-z0-9_]*)\}")]
    private static partial Regex Placeholder();

    private enum QuoteState
    {
        Normal,
        Double,
        Single
    }
}
