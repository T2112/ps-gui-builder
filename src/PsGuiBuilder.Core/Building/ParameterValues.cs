// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Core.Building;

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
