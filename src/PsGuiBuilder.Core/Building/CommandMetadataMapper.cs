// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Core.Building;

public static partial class CommandMetadataMapper
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static CommandDefinition FromSnapshot(
        string json,
        string? parameterSet = null,
        PowerShellEdition shell = PowerShellEdition.PowerShell7) =>
        Read(json, parameterSet, shell).Command;

    public static CommandMetadataView Read(
        string json,
        string? parameterSet = null,
        PowerShellEdition shell = PowerShellEdition.PowerShell7)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Command metadata is empty.", nameof(json));

        MetadataSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<MetadataSnapshot>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Command metadata is not valid JSON: " + ex.Message, nameof(json), ex);
        }

        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.Name))
            throw new ArgumentException("Command metadata is missing the cmdlet name.", nameof(json));

        if (!CmdletName().IsMatch(snapshot.Name))
            throw new ArgumentException($"\"{snapshot.Name}\" is not a Verb-Noun cmdlet name.", nameof(json));

        var setNames = new List<string>();
        string? activeSet = null;
        foreach (var set in snapshot.ParameterSets ?? Array.Empty<MetadataSet>())
        {
            if (set is null || string.IsNullOrWhiteSpace(set.Name))
                continue;

            setNames.Add(set.Name);
            if (activeSet is null && set.IsDefault)
                activeSet = set.Name;
        }

        if (!string.IsNullOrWhiteSpace(parameterSet))
        {
            activeSet = setNames.FirstOrDefault(name => name.Equals(parameterSet, StringComparison.OrdinalIgnoreCase));
            if (activeSet is null)
                throw new ArgumentException($"Parameter set \"{parameterSet}\" is not on this command.");
        }
        else if (activeSet is null && setNames.Count > 0)
        {
            activeSet = setNames[0];
        }

        var parameters = new List<CommandParameter>();
        var common = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Verbose", "Debug", "ErrorAction", "ErrorVariable", "WarningAction", "WarningVariable",
            "InformationAction", "InformationVariable", "OutVariable", "OutBuffer", "PipelineVariable", "ProgressAction"
        };
        var template = new StringBuilder(snapshot.Name);
        var sentence = new StringBuilder("Run " + snapshot.Name);
        var any = false;
        foreach (var parameter in snapshot.Parameters ?? Array.Empty<MetadataParameter>())
        {
            if (parameter is null || string.IsNullOrWhiteSpace(parameter.Name) || !ParameterName().IsMatch(parameter.Name))
                continue;

            if (common.Contains(parameter.Name) || parameters.Any(existing => existing.Name.Equals(parameter.Name, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (activeSet is not null && parameter.Sets is { Length: > 0 }
                && !parameter.Sets.Any(set => set.Equals(activeSet, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var type = MapKind(parameter.Kind);
            var choices = (parameter.Choices ?? Array.Empty<string>())
                .Where(choice => !string.IsNullOrWhiteSpace(choice))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (type == ParameterType.String && choices.Length > 0)
                type = ParameterType.Choice;
            if (type != ParameterType.Choice && type != ParameterType.Array)
                choices = Array.Empty<string>();
            if (type == ParameterType.Choice && choices.Length == 0)
                type = ParameterType.String;

            var required = parameter.MandatorySets is not null && activeSet is not null
                ? parameter.MandatorySets.Any(set => set.Equals(activeSet, StringComparison.OrdinalIgnoreCase))
                : parameter.Mandatory;
            var label = SplitWords(parameter.Name);
            parameters.Add(new CommandParameter(
                parameter.Name,
                label,
                type,
                required,
                placeholder: null,
                defaultValue: null,
                choices));

            if (type == ParameterType.Switch)
                template.Append(" {").Append(parameter.Name).Append('}');
            else
                template.Append(" -").Append(parameter.Name).Append(" {").Append(parameter.Name).Append('}');

            sentence.Append(any ? ", " : " with ");
            sentence.Append(label).Append(" {").Append(parameter.Name).Append('}');
            any = true;
        }

        var module = string.IsNullOrWhiteSpace(snapshot.Module) ? null : snapshot.Module.Trim();
        var synopsis = snapshot.Synopsis?.Trim();
        var description = string.IsNullOrEmpty(synopsis) || synopsis.Equals(snapshot.Name, StringComparison.OrdinalIgnoreCase)
            ? "Looked up from Get-Command. Common parameters are omitted, and types the tree does not model become strings."
            : synopsis;
        var path = activeSet is null ? new[] { snapshot.Name } : new[] { snapshot.Name, activeSet };
        var command = new CommandDefinition(
            snapshot.Name,
            path,
            description,
            snapshot.Name,
            module,
            requiresAdmin: false,
            template.ToString(),
            sentence.ToString(),
            parameters,
            shell);
        return new CommandMetadataView(command, setNames, activeSet);
    }

    private static ParameterType MapKind(string? kind)
    {
        if (kind is null)
            return ParameterType.String;

        if (kind.Equals("switch", StringComparison.OrdinalIgnoreCase))
            return ParameterType.Switch;
        if (kind.Equals("bool", StringComparison.OrdinalIgnoreCase))
            return ParameterType.Bool;
        if (kind.Equals("int", StringComparison.OrdinalIgnoreCase))
            return ParameterType.Int;
        if (kind.Equals("number", StringComparison.OrdinalIgnoreCase))
            return ParameterType.Number;
        if (kind.Equals("securestring", StringComparison.OrdinalIgnoreCase))
            return ParameterType.SecureString;
        if (kind.Equals("array", StringComparison.OrdinalIgnoreCase))
            return ParameterType.Array;
        if (kind.Equals("choice", StringComparison.OrdinalIgnoreCase))
            return ParameterType.Choice;

        return ParameterType.String;
    }

    private static string SplitWords(string name)
    {
        var builder = new StringBuilder(name.Length + 4);
        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (index > 0 && char.IsUpper(character) && !char.IsUpper(name[index - 1]))
                builder.Append(' ');

            builder.Append(index == 0 ? char.ToLowerInvariant(character) : character);
        }

        return builder.ToString();
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]*-[A-Za-z][A-Za-z0-9]*$")]
    private static partial Regex CmdletName();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex ParameterName();

    private sealed class MetadataSnapshot
    {
        public string? Name { get; set; }

        public string? Module { get; set; }

        public string? Synopsis { get; set; }

        public MetadataSet[]? ParameterSets { get; set; }

        public MetadataParameter[]? Parameters { get; set; }
    }

    private sealed class MetadataSet
    {
        public string? Name { get; set; }

        public bool IsDefault { get; set; }
    }

    private sealed class MetadataParameter
    {
        public string? Name { get; set; }

        public string? Kind { get; set; }

        public bool Mandatory { get; set; }

        public string[]? Choices { get; set; }

        public string[]? Sets { get; set; }

        public string[]? MandatorySets { get; set; }
    }
}

public sealed class CommandMetadataView
{
    public CommandMetadataView(CommandDefinition command, IReadOnlyList<string> parameterSets, string? parameterSet)
    {
        Command = command;
        ParameterSets = parameterSets;
        ParameterSet = parameterSet;
    }

    public CommandDefinition Command { get; }

    public IReadOnlyList<string> ParameterSets { get; }

    public string? ParameterSet { get; }
}
