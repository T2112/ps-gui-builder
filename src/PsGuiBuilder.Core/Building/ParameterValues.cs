// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Core.Building;

public sealed class ParameterValue
{
    private ParameterValue(string? text, IReadOnlyList<string>? items)
    {
        Text = text;
        Items = items;
    }

    public string? Text { get; }

    public IReadOnlyList<string>? Items { get; }

    public static ParameterValue FromText(string? text) => new(text, null);

    public static ParameterValue FromItems(IReadOnlyList<string> items) => new(null, items ?? Array.Empty<string>());

    public string TextOrEmpty => Text?.Trim() ?? "";

    public IReadOnlyList<string> ItemList()
    {
        if (Items is not null)
            return Items;

        if (string.IsNullOrWhiteSpace(Text))
            return Array.Empty<string>();

        return Text.Split('\n', StringSplitOptions.None);
    }
}

public static class ParameterValues
{
    public static IReadOnlyDictionary<string, string> Seed(CommandDefinition command)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var parameter in command.Parameters)
            values[parameter.Name] = string.IsNullOrEmpty(parameter.Default) ? "" : parameter.Default;
        return values;
    }
}
