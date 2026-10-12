// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text.RegularExpressions;

namespace PsGuiBuilder.Core.CommandTree;

public static partial class JsonPath
{
    public static string Property(string parent, string name)
    {
        var segment = IsIdentifier(name) ? "." + name : "[" + Quote(name) + "]";
        return parent == "$" && segment.StartsWith('.')
            ? "$" + segment
            : parent + segment;
    }

    public static string Index(string parent, int index) => parent + "[" + index.ToString() + "]";

    private static bool IsIdentifier(string name) => Identifier().IsMatch(name);

    private static string Quote(string name) =>
        "\"" + name.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex Identifier();
}
