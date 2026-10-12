// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Core.Building;

public sealed class CommandBuildResult
{
    public CommandBuildResult(string commandText, IReadOnlyList<string> errors)
    {
        CommandText = commandText;
        Errors = errors;
    }

    public string CommandText { get; }

    public IReadOnlyList<string> Errors { get; }

    public bool IsComplete => Errors.Count == 0;
}

public static class PowerShellCommandBuilder
{
    public static CommandBuildResult Build(CommandDefinition command, IReadOnlyDictionary<string, string?> values)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));
        if (values is null)
            throw new ArgumentNullException(nameof(values));

        var errors = new List<string>();
        foreach (var parameter in command.Parameters)
        {
            values.TryGetValue(parameter.Name, out var raw);
            if (parameter.Required && string.IsNullOrWhiteSpace(raw))
            {
                errors.Add($"Enter a value for \"{parameter.Label}\".");
                continue;
            }

            if (parameter.Type == ParameterType.Choice
                && !string.IsNullOrWhiteSpace(raw)
                && !parameter.Choices.Contains(raw, StringComparer.Ordinal))
            {
                errors.Add($"\"{parameter.Label}\" must be one of: {string.Join(", ", parameter.Choices)}.");
            }
        }

        var syntax = new List<string>();
        var commandText = TemplateScanner.Render(
            command.Template,
            (name, insideDoubleQuotes) =>
            {
                values.TryGetValue(name, out var raw);
                var value = raw ?? "";
                return insideDoubleQuotes
                    ? PowerShellLiteral.EscapeExpandable(value)
                    : PowerShellLiteral.FormatBare(value);
            },
            found: null,
            syntax);

        errors.AddRange(syntax);
        return new CommandBuildResult(commandText, errors);
    }
}
