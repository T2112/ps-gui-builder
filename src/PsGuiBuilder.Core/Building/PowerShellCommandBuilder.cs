// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Core.Building;

public sealed class CommandBuildResult
{
    private static readonly IReadOnlyDictionary<string, string> NoSecrets =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public CommandBuildResult(
        string commandText,
        string executionText,
        IReadOnlyList<string> errors,
        IReadOnlyDictionary<string, string>? secrets = null)
    {
        CommandText = commandText;
        ExecutionText = executionText;
        Errors = errors;
        Secrets = secrets is null || secrets.Count == 0 ? NoSecrets : secrets;
    }

    public string CommandText { get; }

    public string ExecutionText { get; }

    public IReadOnlyList<string> Errors { get; }

    public IReadOnlyDictionary<string, string> Secrets { get; }

    public bool IsComplete => Errors.Count == 0;
}

public static class PowerShellCommandBuilder
{
    public static CommandBuildResult Build(CommandDefinition command, IReadOnlyDictionary<string, string?> values)
    {
        if (values is null)
            throw new ArgumentNullException(nameof(values));

        var mapped = new Dictionary<string, ParameterValue>(StringComparer.Ordinal);
        foreach (var pair in values)
            mapped[pair.Key] = ParameterValue.FromText(pair.Value);

        return Build(command, mapped);
    }

    public static CommandBuildResult Build(CommandDefinition command, IReadOnlyDictionary<string, ParameterValue> values)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));
        if (values is null)
            throw new ArgumentNullException(nameof(values));

        var errors = new List<string>();
        var bound = new Dictionary<string, BoundParameter>(StringComparer.Ordinal);
        foreach (var parameter in command.Parameters)
        {
            values.TryGetValue(parameter.Name, out var input);
            bound[parameter.Name] = Bind(parameter, input ?? ParameterValue.FromText(null), errors);
        }

        var syntax = new List<string>();
        var uses = TemplateScanner.FindPlaceholders(command.Template, syntax);
        var ranges = new List<(int Start, int End)>();
        foreach (var use in uses)
        {
            if (!bound.TryGetValue(use.Name, out var parameter) || !parameter.Omit)
                continue;

            if (TemplateScanner.TryGetRemovalRange(command.Template, use, out var start, out var end))
            {
                ranges.Add((start, end));
                continue;
            }

            parameter.Omit = false;
            errors.Add($"\"{parameter.Label}\" is blank and sits inside other template text, so the parameter cannot be dropped. Enter a value.");
        }

        var kept = TemplateScanner.ApplyRemovals(command.Template, ranges);
        var previewSyntax = new List<string>();
        var previewText = TemplateScanner.Render(
            kept,
            (name, insideQuotes) => Render(bound, name, insideQuotes, redactSecrets: true),
            found: null,
            previewSyntax);
        // Placeholder syntax is identical for both renders; only the substitution differs.
        var executionText = TemplateScanner.Render(
            kept,
            (name, insideQuotes) => Render(bound, name, insideQuotes, redactSecrets: false),
            found: null,
            syntaxErrors: []);

        errors.AddRange(syntax);
        errors.AddRange(previewSyntax);
        var secrets = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in bound)
        {
            if (pair.Value.Secret is not null)
                secrets[pair.Key] = pair.Value.Secret;
        }

        return new CommandBuildResult(previewText, executionText, errors, secrets);
    }

    private static BoundParameter Bind(CommandParameter parameter, ParameterValue input, List<string> errors)
    {
        var bound = new BoundParameter(parameter);
        switch (parameter.Type)
        {
            case ParameterType.Switch:
                BindSwitch(bound, input.TextOrEmpty, errors);
                break;
            case ParameterType.Bool:
                BindBool(bound, input.TextOrEmpty, errors);
                break;
            case ParameterType.Int:
            case ParameterType.Number:
                BindNumber(bound, input.TextOrEmpty, errors);
                break;
            case ParameterType.SecureString:
                BindSecret(bound, input.Text ?? "", errors);
                break;
            case ParameterType.Array:
                BindArray(bound, input.ItemList(), errors);
                break;
            default:
                BindText(bound, input.TextOrEmpty, errors);
                break;
        }

        return bound;
    }

    private static void BindSwitch(BoundParameter bound, string text, List<string> errors)
    {
        var on = ParameterSyntax.IsTrue(text);
        if (!on && text.Length > 0 && !text.Equals("false", StringComparison.OrdinalIgnoreCase))
            errors.Add($"\"{bound.Label}\" must be on or off.");

        if (bound.Parameter.Required && !on)
            errors.Add($"Turn on \"{bound.Label}\".");

        bound.Omit = !on;
        bound.Literal = "-" + bound.Parameter.Name;
    }

    private static void BindBool(BoundParameter bound, string text, List<string> errors)
    {
        if (text.Length == 0)
        {
            if (bound.Parameter.Required)
                errors.Add($"Choose true or false for \"{bound.Label}\".");
            else
                bound.Omit = true;

            return;
        }

        if (!ParameterSyntax.TryParseBool(text, out var value))
        {
            errors.Add($"\"{bound.Label}\" must be true or false.");
            bound.Literal = PowerShellLiteral.FormatBare(text);
            return;
        }

        bound.Literal = value ? "$true" : "$false";
    }

    private static void BindNumber(BoundParameter bound, string text, List<string> errors)
    {
        if (text.Length == 0)
        {
            if (bound.Parameter.Required)
                errors.Add($"Enter a value for \"{bound.Label}\".");
            else
                bound.Omit = true;

            return;
        }

        var integer = bound.Parameter.Type == ParameterType.Int;
        var parsed = integer
            ? ParameterSyntax.TryParseInt(text, out var rendered)
            : ParameterSyntax.TryParseNumber(text, out rendered);
        if (!parsed)
        {
            errors.Add(integer
                ? $"\"{bound.Label}\" must be an integer."
                : $"\"{bound.Label}\" must be a number.");
            bound.Literal = PowerShellLiteral.FormatBare(text);
            return;
        }

        bound.Literal = rendered;
    }

    private static void BindSecret(BoundParameter bound, string text, List<string> errors)
    {
        if (text.Length == 0)
        {
            if (bound.Parameter.Required)
                errors.Add($"Enter a value for \"{bound.Label}\".");
            else
                bound.Omit = true;

            return;
        }

        bound.Secret = text;
        bound.Literal = ParameterSyntax.SecretVariable(bound.Parameter.Name);
    }

    private static void BindArray(BoundParameter bound, IReadOnlyList<string> items, List<string> errors)
    {
        var values = new List<string>();
        foreach (var item in items)
        {
            var trimmed = item.Trim();
            if (trimmed.Length == 0)
                continue;

            if (bound.Parameter.Choices.Count > 0 && !bound.Parameter.Choices.Contains(trimmed, StringComparer.Ordinal))
                errors.Add($"\"{bound.Label}\" must be one of: {string.Join(", ", bound.Parameter.Choices)}.");

            values.Add(PowerShellLiteral.FormatBare(trimmed));
        }

        if (values.Count == 0)
        {
            if (bound.Parameter.Required)
                errors.Add($"Enter a value for \"{bound.Label}\".");
            else
                bound.Omit = true;

            return;
        }

        bound.Literal = string.Join(", ", values);
    }

    private static void BindText(BoundParameter bound, string text, List<string> errors)
    {
        bound.RawText = text;
        if (text.Length == 0)
        {
            if (bound.Parameter.Required)
                errors.Add($"Enter a value for \"{bound.Label}\".");
            else
                bound.Omit = true;

            return;
        }

        if (bound.Parameter.Type == ParameterType.Choice && !bound.Parameter.Choices.Contains(text, StringComparer.Ordinal))
            errors.Add($"\"{bound.Label}\" must be one of: {string.Join(", ", bound.Parameter.Choices)}.");
    }

    private static string Render(
        IReadOnlyDictionary<string, BoundParameter> bound,
        string name,
        bool insideDoubleQuotes,
        bool redactSecrets)
    {
        if (!bound.TryGetValue(name, out var parameter))
            return "''";

        if (parameter.Parameter.Type is ParameterType.String or ParameterType.Choice)
        {
            return insideDoubleQuotes
                ? PowerShellLiteral.EscapeExpandable(parameter.RawText)
                : PowerShellLiteral.FormatBare(parameter.RawText);
        }

        if (parameter.Secret is not null && redactSecrets)
            return "***";

        return parameter.Literal ?? "''";
    }

    private sealed class BoundParameter
    {
        public BoundParameter(CommandParameter parameter) => Parameter = parameter;

        public CommandParameter Parameter { get; }

        public string Label => Parameter.Label;

        public bool Omit { get; set; }

        public string RawText { get; set; } = "";

        public string? Literal { get; set; }

        public string? Secret { get; set; }
    }
}
