// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text;
using System.Text.RegularExpressions;

namespace PsGuiBuilder.Core.Building;

public static partial class PowerShellLiteral
{
    public static string EscapeExpandable(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            switch (character)
            {
                case '\0':
                    break;
                case '`':
                    builder.Append("``");
                    break;
                case '"':
                    builder.Append("`\"");
                    break;
                case '$':
                    builder.Append("`$");
                    break;
                case '\r':
                    builder.Append("`r");
                    break;
                case '\n':
                    builder.Append("`n");
                    break;
                case '\t':
                    builder.Append("`t");
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        return builder.ToString();
    }

    public static string FormatBare(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "''";

        if (SafeIdentifier().IsMatch(value))
            return value;

        return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex SafeIdentifier();
}
