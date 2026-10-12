// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Globalization;
using System.Text;

namespace PsGuiBuilder.Core.Building;

public static class ParameterSyntax
{
    public static bool IsTrue(string? text) =>
        text is not null && text.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);

    public static bool TryParseBool(string? text, out bool value)
    {
        value = false;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        if (trimmed.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            return true;
        }

        return false;
    }

    public static bool TryParseInt(string? text, out string rendered)
    {
        rendered = "";
        if (!long.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            return false;

        rendered = number.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    public static bool TryParseNumber(string? text, out string rendered)
    {
        rendered = "";
        if (!decimal.TryParse(
                text?.Trim(),
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var number))
        {
            return false;
        }

        rendered = number.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    public static string SecretVariable(string name) => "$__psgui_secret_" + name;

    public static string QuoteSingle(string? value)
    {
        var builder = new StringBuilder((value?.Length ?? 0) + 2);
        builder.Append('\'');
        if (value is not null)
        {
            foreach (var character in value)
            {
                if (character == '\0')
                    continue;

                if (character == '\'')
                    builder.Append("''");
                else
                    builder.Append(character);
            }
        }

        builder.Append('\'');
        return builder.ToString();
    }
}
