// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace PsGuiBuilder.Core.CommandTree;

public sealed class CommandTreeMergeResult
{
    public CommandTreeMergeResult(int commandsAdded, IReadOnlyList<string> addedNames, string json)
    {
        CommandsAdded = commandsAdded;
        AddedNames = addedNames;
        Json = json;
    }

    public int CommandsAdded { get; }

    public IReadOnlyList<string> AddedNames { get; }

    public string Json { get; }
}

public static class CommandTreeMerger
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static CommandTreeMergeResult Merge(string userJson, string defaultJson)
    {
        if (userJson is null)
            throw new ArgumentNullException(nameof(userJson));
        if (defaultJson is null)
            throw new ArgumentNullException(nameof(defaultJson));

        var user = ParseObject(userJson);
        var defaults = ParseObject(defaultJson);
        var added = new List<string>();
        var count = 0;
        if (defaults["categories"] is JsonObject defaultCategories && user["categories"] is JsonObject userCategories)
            count = MergeNodes(userCategories, defaultCategories, added);

        return new CommandTreeMergeResult(count, added, user.ToJsonString(Indented) + "\n");
    }

    public static CommandTreeMergeResult MergeFiles(string userPath, string defaultPath)
    {
        if (string.IsNullOrWhiteSpace(userPath))
            throw new ArgumentException("The personal command tree path is required.", nameof(userPath));
        if (string.IsNullOrWhiteSpace(defaultPath))
            throw new ArgumentException("The default command tree path is required.", nameof(defaultPath));

        var userJson = File.ReadAllText(userPath);
        var defaultJson = File.ReadAllText(defaultPath);
        CommandTreeLoader.Load(StripBom(userJson));
        CommandTreeLoader.Load(StripBom(defaultJson));
        var result = Merge(userJson, defaultJson);
        if (result.CommandsAdded == 0)
            return result;

        CommandTreeLoader.Load(result.Json);
        File.WriteAllText(userPath, result.Json);
        return result;
    }

    private static int MergeNodes(JsonObject target, JsonObject incoming, List<string> added)
    {
        var count = 0;
        foreach (var pair in incoming)
        {
            if (pair.Value is null)
                continue;

            if (!target.ContainsKey(pair.Key))
            {
                var clone = pair.Value.DeepClone();
                target[pair.Key] = clone;
                count += CollectCommands(clone, pair.Key, added);
                continue;
            }

            if (target[pair.Key] is JsonObject existing
                && pair.Value is JsonObject source
                && existing["options"] is JsonObject existingOptions
                && source["options"] is JsonObject sourceOptions
                && !existing.ContainsKey("cmdlet")
                && !source.ContainsKey("cmdlet"))
            {
                count += MergeNodes(existingOptions, sourceOptions, added);
            }
        }

        return count;
    }

    private static int CollectCommands(JsonNode node, string name, List<string> added)
    {
        if (node is not JsonObject obj)
            return 0;

        if (obj.ContainsKey("cmdlet"))
        {
            added.Add(name);
            return 1;
        }

        if (obj["options"] is not JsonObject options)
            return 0;

        var count = 0;
        foreach (var child in options)
        {
            if (child.Value is null)
                continue;

            count += CollectCommands(child.Value, child.Key, added);
        }

        return count;
    }

    private static JsonObject ParseObject(string json)
    {
        var node = JsonNode.Parse(StripBom(json));
        if (node is not JsonObject obj)
            throw new CommandTreeException(new[] { new CommandTreeError("$", "The command tree root must be a JSON object.") });

        return obj;
    }

    private static string StripBom(string json) =>
        json.Length > 0 && json[0] == '\uFEFF' ? json[1..] : json;
}
