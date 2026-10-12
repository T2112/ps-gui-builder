// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text.RegularExpressions;
using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Core.Building;

public abstract class SentenceSegment
{
    private SentenceSegment()
    {
    }

    public sealed class Text : SentenceSegment
    {
        public Text(string value) => Value = value;

        public string Value { get; }
    }

    public sealed class Field : SentenceSegment
    {
        public Field(CommandParameter parameter) => Parameter = parameter;

        public CommandParameter Parameter { get; }
    }
}

public sealed partial class SentenceForm
{
    private SentenceForm(IReadOnlyList<SentenceSegment> segments) => Segments = segments;

    public IReadOnlyList<SentenceSegment> Segments { get; }

    public static SentenceForm Parse(CommandDefinition command)
    {
        var errors = new List<string>();
        var byName = command.Parameters.ToDictionary(parameter => parameter.Name, StringComparer.Ordinal);
        var segments = ParseSegments(command.Sentence, byName, errors);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));

        return new SentenceForm(segments);
    }

    internal static IReadOnlyList<string> FindPlaceholders(string sentence, IList<string> syntaxErrors)
    {
        var names = new List<string>();
        Walk(sentence, (name, _, _) => names.Add(name), syntaxErrors);
        return names;
    }

    private static IReadOnlyList<SentenceSegment> ParseSegments(
        string sentence,
        IReadOnlyDictionary<string, CommandParameter> parameters,
        IList<string> syntaxErrors)
    {
        var segments = new List<SentenceSegment>();
        Walk(
            sentence,
            (name, _, _) =>
            {
                if (!parameters.TryGetValue(name, out var parameter))
                {
                    syntaxErrors.Add($"Placeholder \"{{{name}}}\" does not match a parameter.");
                    return;
                }

                segments.Add(new SentenceSegment.Field(parameter));
            },
            syntaxErrors,
            segments);
        return segments;
    }

    private static void Walk(
        string sentence,
        Action<string, int, int> onPlaceholder,
        IList<string> syntaxErrors,
        List<SentenceSegment>? segments = null)
    {
        var index = 0;
        var textStart = 0;
        while (index < sentence.Length)
        {
            if (sentence[index] != '{')
            {
                index++;
                continue;
            }

            var match = Placeholder().Match(sentence, index);
            if (!match.Success || match.Index != index)
            {
                syntaxErrors.Add("Invalid or unclosed '{' in the sentence.");
                index++;
                continue;
            }

            if (segments is not null && index > textStart)
                segments.Add(new SentenceSegment.Text(sentence[textStart..index]));

            onPlaceholder(match.Groups[1].Value, index, match.Length);
            index += match.Length;
            textStart = index;
        }

        if (segments is not null && textStart < sentence.Length)
            segments.Add(new SentenceSegment.Text(sentence[textStart..]));
    }

    [GeneratedRegex(@"\{([A-Za-z_][A-Za-z0-9_]*)\}")]
    private static partial Regex Placeholder();
}
