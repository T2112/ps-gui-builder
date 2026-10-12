// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text;
using System.Text.RegularExpressions;

namespace PsGuiBuilder.Core.Building;

internal readonly record struct PlaceholderUse(string Name, bool InsideDoubleQuotes);

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

                    found?.Add(new PlaceholderUse(name, InsideDoubleQuotes: false));
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

                    found?.Add(new PlaceholderUse(name, InsideDoubleQuotes: true));
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

    [GeneratedRegex(@"\{([A-Za-z_][A-Za-z0-9_]*)\}")]
    private static partial Regex Placeholder();

    private enum QuoteState
    {
        Normal,
        Double,
        Single
    }
}
