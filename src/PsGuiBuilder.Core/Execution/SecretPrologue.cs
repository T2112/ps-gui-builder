// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text;
using System.Text.RegularExpressions;
using PsGuiBuilder.Core.Building;

namespace PsGuiBuilder.Core.Execution;

public static partial class SecretPrologue
{
    public static string Apply(string script, IReadOnlyDictionary<string, string> secrets)
    {
        if (secrets.Count == 0)
            return script;

        var builder = new StringBuilder();
        foreach (var pair in secrets.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (!Identifier().IsMatch(pair.Key))
                throw new InvalidOperationException("A secret name is not a PowerShell identifier.");

            builder.Append(ParameterSyntax.SecretVariable(pair.Key));
            builder.Append(" = ConvertTo-SecureString -AsPlainText -Force -String ");
            builder.AppendLine(ParameterSyntax.QuoteSingle(pair.Value));
        }

        builder.Append(script);
        return builder.ToString();
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex Identifier();
}
