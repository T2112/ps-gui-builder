// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text.Json;
using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Core.Discovery;

public sealed class DiscoveredModule
{
    public DiscoveredModule(string name, string version)
    {
        Name = name;
        Version = version;
    }

    public string Name { get; }

    public string Version { get; }
}

public sealed class DiscoveredCommand
{
    public DiscoveredCommand(string name, string module, string verb, string noun, string commandType)
    {
        Name = name;
        Module = module;
        Verb = verb;
        Noun = noun;
        CommandType = commandType;
    }

    public string Name { get; }

    public string Module { get; }

    public string Verb { get; }

    public string Noun { get; }

    public string CommandType { get; }
}

public sealed class DiscoveryCatalog
{
    public DiscoveryCatalog(
        PowerShellEdition edition,
        DateTimeOffset capturedUtc,
        IReadOnlyList<DiscoveredModule> modules,
        IReadOnlyList<DiscoveredCommand> commands)
    {
        Edition = edition;
        CapturedUtc = capturedUtc;
        Modules = modules;
        Commands = commands;
    }

    public PowerShellEdition Edition { get; }

    public DateTimeOffset CapturedUtc { get; }

    public IReadOnlyList<DiscoveredModule> Modules { get; }

    public IReadOnlyList<DiscoveredCommand> Commands { get; }

    public static DiscoveryCatalog Parse(string json, PowerShellEdition edition, DateTimeOffset? capturedUtc = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Discovery data is empty.", nameof(json));

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Discovery data is not valid JSON.", nameof(json), ex);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Discovery data must be a JSON object.", nameof(json));

            var modules = ReadModules(document.RootElement);
            var commands = ReadCommands(document.RootElement);
            return new DiscoveryCatalog(edition, capturedUtc ?? DateTimeOffset.UtcNow, modules, commands);
        }
    }

    private static IReadOnlyList<DiscoveredModule> ReadModules(JsonElement root)
    {
        if (!root.TryGetProperty("modules", out var modules))
            return Array.Empty<DiscoveredModule>();

        var list = new List<DiscoveredModule>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Enumerate(modules))
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var name = Text(item, "name");
            if (!IsSafeName(name) || !seen.Add(name))
                continue;

            list.Add(new DiscoveredModule(name, Text(item, "version")));
        }

        list.Sort(static (left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    private static IReadOnlyList<DiscoveredCommand> ReadCommands(JsonElement root)
    {
        if (!root.TryGetProperty("commands", out var commands))
            return Array.Empty<DiscoveredCommand>();

        var list = new List<DiscoveredCommand>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Enumerate(commands))
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var name = Text(item, "name");
            var module = Text(item, "module");
            var commandType = Text(item, "commandType");
            if (commandType.Length == 0)
                commandType = "Cmdlet";
            if (!IsSafeName(name))
                continue;
            if (name.IndexOf('-') < 1 && !commandType.Equals("Alias", StringComparison.OrdinalIgnoreCase))
                continue;

            var key = module + "\\" + name;
            if (!seen.Add(key))
                continue;

            var verb = Text(item, "verb");
            var noun = Text(item, "noun");
            if (verb.Length == 0 || noun.Length == 0)
            {
                var dash = name.IndexOf('-');
                if (dash > 0)
                {
                    if (verb.Length == 0)
                        verb = name[..dash];
                    if (noun.Length == 0)
                        noun = name[(dash + 1)..];
                }
            }

            list.Add(new DiscoveredCommand(name, module, verb, noun, commandType));
        }

        return list;
    }

    private static IEnumerable<JsonElement> Enumerate(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                yield return item;
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            yield return element;
        }
    }

    private static string Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            return "";

        return property.GetString()?.Trim() ?? "";
    }

    private static bool IsSafeName(string name) =>
        name.Length > 0
        && name.Length <= 128
        && name.IndexOfAny(new[] { '\'', '"', '`', ';', '|', '&', '$', '\r', '\n', '\\' }) < 0;
}
