// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PsGuiBuilder.Core.Building;

namespace PsGuiBuilder.Core.CommandTree;

public static partial class CommandTreeLoader
{
    private static readonly HashSet<string> RootProperties = new(StringComparer.Ordinal)
    {
        "version", "description", "categories"
    };

    private static readonly HashSet<string> NodeProperties = new(StringComparer.Ordinal)
    {
        "description", "options", "cmdlet", "module", "requiresAdmin", "template", "sentence", "parameters"
    };

    private static readonly HashSet<string> ParameterProperties = new(StringComparer.Ordinal)
    {
        "name", "label", "type", "placeholder", "required", "choices", "default"
    };

    public static CommandTree LoadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A command tree path is required.", nameof(path));

        if (!File.Exists(path))
        {
            throw new CommandTreeException(new[]
            {
                new CommandTreeError(path, "Command tree file was not found.")
            });
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CommandTreeException(new[]
            {
                new CommandTreeError(path, "Could not read the command tree: " + ex.Message)
            });
        }

        try
        {
            return Load(json);
        }
        catch (CommandTreeException ex)
        {
            throw new CommandTreeException(ex.Errors
                .Select(error => error with { Path = path + ":" + error.Path })
                .ToArray());
        }
    }

    public static CommandTree Load(string json)
    {
        if (json is null)
            throw new ArgumentNullException(nameof(json));

        if (json.Length > 0 && json[0] == '\uFEFF')
            json = json[1..];

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new CommandTreeException(new[]
            {
                new CommandTreeError("$", "Command tree JSON is empty.")
            });
        }

        var errors = new List<CommandTreeError>();
        JsonNode? root;
        try
        {
            DuplicateKeyChecker.Check(json, errors);
            if (errors.Count > 0)
                throw new CommandTreeException(errors);

            root = JsonNode.Parse(json);
        }
        catch (CommandTreeException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new CommandTreeException(new[]
            {
                new CommandTreeError("$", "JSON is not valid: " + ex.Message)
            });
        }

        if (root is not JsonObject rootObject)
        {
            throw new CommandTreeException(new[]
            {
                new CommandTreeError("$", "The command tree root must be a JSON object.")
            });
        }

        RejectUnknown(rootObject, RootProperties, "$", errors);
        var version = ReadRequiredString(rootObject, "version", "$", errors);
        var description = ReadOptionalString(rootObject, "description", "$", errors);

        var categories = new List<CommandNode>();
        if (!rootObject.TryGetPropertyValue("categories", out var categoriesNode) || categoriesNode is null)
        {
            errors.Add(new CommandTreeError("$.categories", "\"categories\" is required and must be an object."));
        }
        else if (categoriesNode is not JsonObject categoriesObject)
        {
            errors.Add(new CommandTreeError("$.categories", "\"categories\" must be an object."));
        }
        else if (categoriesObject.Count == 0)
        {
            errors.Add(new CommandTreeError("$.categories", "Add at least one category."));
        }
        else
        {
            foreach (var category in categoriesObject)
            {
                var categoryPath = JsonPath.Property("$.categories", category.Key);
                if (string.IsNullOrWhiteSpace(category.Key))
                {
                    errors.Add(new CommandTreeError(categoryPath, "Category names cannot be empty."));
                    continue;
                }

                var node = ParseNode(category.Value, categoryPath, category.Key, new List<string> { category.Key }, errors);
                if (node is not null)
                    categories.Add(node);
            }
        }

        if (errors.Count > 0)
            throw new CommandTreeException(errors);

        var rootNode = new CommandNode("", description, categories, command: null);
        return new CommandTree(version!, description, rootNode);
    }

    private static CommandNode? ParseNode(
        JsonNode? node,
        string path,
        string name,
        IReadOnlyList<string> namePath,
        List<CommandTreeError> errors)
    {
        if (node is not JsonObject obj)
        {
            errors.Add(new CommandTreeError(path, "Expected an object."));
            return null;
        }

        RejectUnknown(obj, NodeProperties, path, errors);
        var hasOptions = obj.ContainsKey("options");
        var hasCommandShape = obj.ContainsKey("cmdlet")
            || obj.ContainsKey("module")
            || obj.ContainsKey("requiresAdmin")
            || obj.ContainsKey("template")
            || obj.ContainsKey("sentence")
            || obj.ContainsKey("parameters");

        if (hasOptions && hasCommandShape)
        {
            errors.Add(new CommandTreeError(path, "A node must be either a branch (\"options\") or a command (\"cmdlet\"), not both."));
            return null;
        }

        if (!hasOptions && !obj.ContainsKey("cmdlet"))
        {
            errors.Add(new CommandTreeError(path, "A node must contain either \"options\" or \"cmdlet\"."));
            return null;
        }

        var description = ReadOptionalString(obj, "description", path, errors);
        if (hasOptions)
        {
            var children = ParseChildren(obj, path, namePath, errors);
            return children is null ? null : new CommandNode(name, description, children, command: null);
        }

        var command = ParseCommand(obj, path, name, namePath, description, errors);
        return command is null ? null : new CommandNode(name, description, Array.Empty<CommandNode>(), command);
    }

    private static IReadOnlyList<CommandNode>? ParseChildren(
        JsonObject obj,
        string path,
        IReadOnlyList<string> namePath,
        List<CommandTreeError> errors)
    {
        var optionsPath = JsonPath.Property(path, "options");
        if (obj["options"] is not JsonObject options)
        {
            errors.Add(new CommandTreeError(optionsPath, "\"options\" must be an object."));
            return null;
        }

        if (options.Count == 0)
        {
            errors.Add(new CommandTreeError(optionsPath, "A branch must contain at least one option."));
            return null;
        }

        var children = new List<CommandNode>();
        foreach (var child in options)
        {
            var childPath = JsonPath.Property(optionsPath, child.Key);
            if (string.IsNullOrWhiteSpace(child.Key))
            {
                errors.Add(new CommandTreeError(childPath, "Option names cannot be empty."));
                continue;
            }

            var childPathNames = new List<string>(namePath) { child.Key };
            var parsed = ParseNode(child.Value, childPath, child.Key, childPathNames, errors);
            if (parsed is not null)
                children.Add(parsed);
        }

        return children;
    }

    private static CommandDefinition? ParseCommand(
        JsonObject obj,
        string path,
        string name,
        IReadOnlyList<string> namePath,
        string? description,
        List<CommandTreeError> errors)
    {
        var cmdlet = ReadRequiredString(obj, "cmdlet", path, errors);
        if (cmdlet is not null && !CmdletName().IsMatch(cmdlet))
        {
            errors.Add(new CommandTreeError(
                JsonPath.Property(path, "cmdlet"),
                "\"cmdlet\" must look like Verb-Noun (letters and digits only)."));
        }

        var module = ReadOptionalString(obj, "module", path, errors);
        if (module is not null && !ModuleName().IsMatch(module))
        {
            errors.Add(new CommandTreeError(
                JsonPath.Property(path, "module"),
                "\"module\" must be a module name made of letters, digits, and dots."));
        }

        var requiresAdmin = false;
        if (obj.ContainsKey("requiresAdmin"))
        {
            if (!TryReadBool(obj["requiresAdmin"], out requiresAdmin))
            {
                errors.Add(new CommandTreeError(
                    JsonPath.Property(path, "requiresAdmin"),
                    "\"requiresAdmin\" must be true or false."));
            }
        }

        var template = ReadRequiredString(obj, "template", path, errors);
        var sentence = ReadRequiredString(obj, "sentence", path, errors);
        var parameters = ParseParameters(obj, path, errors);
        if (cmdlet is null || template is null || sentence is null || parameters is null)
            return null;

        var byName = parameters.ToDictionary(parameter => parameter.Name, StringComparer.Ordinal);
        ValidateTemplate(template, path, byName, errors);
        ValidateSentence(sentence, path, byName, errors);

        return new CommandDefinition(
            name,
            namePath.ToArray(),
            description,
            cmdlet,
            module,
            requiresAdmin,
            template,
            sentence,
            parameters);
    }

    private static void ValidateTemplate(
        string template,
        string path,
        IReadOnlyDictionary<string, CommandParameter> parameters,
        List<CommandTreeError> errors)
    {
        var syntax = new List<string>();
        var uses = TemplateScanner.FindPlaceholders(template, syntax);
        var templatePath = JsonPath.Property(path, "template");
        foreach (var problem in syntax)
            errors.Add(new CommandTreeError(templatePath, problem));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var use in uses)
        {
            if (!parameters.ContainsKey(use.Name))
            {
                errors.Add(new CommandTreeError(templatePath, $"Placeholder \"{{{use.Name}}}\" does not match a parameter."));
            }

            seen.Add(use.Name);
        }

        foreach (var parameter in parameters.Values)
        {
            if (!seen.Contains(parameter.Name))
            {
                errors.Add(new CommandTreeError(
                    templatePath,
                    $"Parameter \"{parameter.Name}\" is not used in the template."));
            }
        }
    }

    private static void ValidateSentence(
        string sentence,
        string path,
        IReadOnlyDictionary<string, CommandParameter> parameters,
        List<CommandTreeError> errors)
    {
        var syntax = new List<string>();
        var names = SentenceForm.FindPlaceholders(sentence, syntax);
        var sentencePath = JsonPath.Property(path, "sentence");
        foreach (var problem in syntax)
            errors.Add(new CommandTreeError(sentencePath, problem));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var placeholder in names)
        {
            if (!parameters.ContainsKey(placeholder))
            {
                errors.Add(new CommandTreeError(sentencePath, $"Placeholder \"{{{placeholder}}}\" does not match a parameter."));
                continue;
            }

            if (!seen.Add(placeholder))
            {
                errors.Add(new CommandTreeError(sentencePath, $"Placeholder \"{{{placeholder}}}\" is repeated. Use each parameter once in the sentence."));
            }
        }

        foreach (var parameter in parameters.Values)
        {
            if (!seen.Contains(parameter.Name))
            {
                errors.Add(new CommandTreeError(
                    sentencePath,
                    $"Parameter \"{parameter.Name}\" is not used in the sentence."));
            }
        }
    }

    private static IReadOnlyList<CommandParameter>? ParseParameters(
        JsonObject obj,
        string path,
        List<CommandTreeError> errors)
    {
        var parametersPath = JsonPath.Property(path, "parameters");
        if (!obj.TryGetPropertyValue("parameters", out var node) || node is null)
        {
            errors.Add(new CommandTreeError(parametersPath, "\"parameters\" is required and must be an array."));
            return null;
        }

        if (node is not JsonArray array)
        {
            errors.Add(new CommandTreeError(parametersPath, "\"parameters\" must be an array."));
            return null;
        }

        var parameters = new List<CommandParameter>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < array.Count; index++)
        {
            var parameterPath = JsonPath.Index(parametersPath, index);
            if (array[index] is not JsonObject parameterObject)
            {
                errors.Add(new CommandTreeError(parameterPath, "A parameter must be an object."));
                continue;
            }

            var parameter = ParseParameter(parameterObject, parameterPath, errors);
            if (parameter is null)
                continue;

            if (!names.Add(parameter.Name))
            {
                errors.Add(new CommandTreeError(parameterPath, $"Duplicate parameter name \"{parameter.Name}\"."));
                continue;
            }

            parameters.Add(parameter);
        }

        return parameters;
    }

    private static CommandParameter? ParseParameter(JsonObject obj, string path, List<CommandTreeError> errors)
    {
        RejectUnknown(obj, ParameterProperties, path, errors);
        var name = ReadRequiredString(obj, "name", path, errors);
        var label = ReadRequiredString(obj, "label", path, errors);
        var typeText = ReadRequiredString(obj, "type", path, errors);
        var placeholder = ReadOptionalString(obj, "placeholder", path, errors);
        var defaultValue = ReadOptionalString(obj, "default", path, errors);

        if (!obj.ContainsKey("required"))
        {
            errors.Add(new CommandTreeError(JsonPath.Property(path, "required"), "\"required\" must be true or false."));
        }

        var required = false;
        if (obj.ContainsKey("required") && !TryReadBool(obj["required"], out required))
        {
            errors.Add(new CommandTreeError(JsonPath.Property(path, "required"), "\"required\" must be true or false."));
        }

        if (name is not null && !ParameterName().IsMatch(name))
        {
            errors.Add(new CommandTreeError(
                JsonPath.Property(path, "name"),
                "\"name\" must be a placeholder identifier (letters, digits, and underscore, not starting with a digit)."));
        }

        ParameterType? type = null;
        if (typeText is not null)
        {
            if (typeText.Equals("string", StringComparison.OrdinalIgnoreCase))
                type = ParameterType.String;
            else if (typeText.Equals("choice", StringComparison.OrdinalIgnoreCase))
                type = ParameterType.Choice;
            else
            {
                errors.Add(new CommandTreeError(
                    JsonPath.Property(path, "type"),
                    "\"type\" must be \"string\" or \"choice\"."));
            }
        }

        var choices = Array.Empty<string>();
        if (type == ParameterType.Choice)
            choices = ReadChoices(obj, path, errors);
        else if (obj.ContainsKey("choices"))
        {
            errors.Add(new CommandTreeError(JsonPath.Property(path, "choices"), "\"choices\" is only valid when type is \"choice\"."));
        }

        if (type == ParameterType.Choice && defaultValue is not null && choices.Length > 0 && !choices.Contains(defaultValue, StringComparer.Ordinal))
        {
            errors.Add(new CommandTreeError(
                JsonPath.Property(path, "default"),
                $"Default \"{defaultValue}\" is not one of the choices."));
        }

        if (name is null || label is null || type is null)
            return null;

        if (label.Length == 0)
        {
            errors.Add(new CommandTreeError(JsonPath.Property(path, "label"), "\"label\" cannot be empty."));
            return null;
        }

        return new CommandParameter(name, label, type.Value, required, placeholder, defaultValue, choices);
    }

    private static string[] ReadChoices(JsonObject obj, string path, List<CommandTreeError> errors)
    {
        var choicesPath = JsonPath.Property(path, "choices");
        if (!obj.TryGetPropertyValue("choices", out var node) || node is null)
        {
            errors.Add(new CommandTreeError(choicesPath, "A choice parameter needs a non-empty \"choices\" array."));
            return Array.Empty<string>();
        }

        if (node is not JsonArray array || array.Count == 0)
        {
            errors.Add(new CommandTreeError(choicesPath, "\"choices\" must be a non-empty array of strings."));
            return Array.Empty<string>();
        }

        var choices = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < array.Count; index++)
        {
            var itemPath = JsonPath.Index(choicesPath, index);
            if (array[index] is not JsonValue value || !value.TryGetValue<string>(out var choice) || string.IsNullOrEmpty(choice))
            {
                errors.Add(new CommandTreeError(itemPath, "Each choice must be a non-empty string."));
                continue;
            }

            if (!seen.Add(choice))
            {
                errors.Add(new CommandTreeError(itemPath, $"Duplicate choice \"{choice}\"."));
                continue;
            }

            choices.Add(choice);
        }

        return choices.ToArray();
    }

    private static void RejectUnknown(JsonObject obj, ISet<string> allowed, string path, List<CommandTreeError> errors)
    {
        foreach (var property in obj)
        {
            if (!allowed.Contains(property.Key))
            {
                errors.Add(new CommandTreeError(path, $"Unknown property \"{property.Key}\"."));
            }
        }
    }

    private static string? ReadRequiredString(JsonObject obj, string propertyName, string path, List<CommandTreeError> errors)
    {
        var propertyPath = JsonPath.Property(path, propertyName);
        if (!obj.ContainsKey(propertyName))
        {
            errors.Add(new CommandTreeError(propertyPath, $"\"{propertyName}\" is required and must be a non-empty string."));
            return null;
        }

        var text = ReadString(obj[propertyName], propertyPath, propertyName, errors);
        if (text is not null && text.Length == 0)
        {
            errors.Add(new CommandTreeError(propertyPath, $"\"{propertyName}\" cannot be empty."));
            return null;
        }

        return text;
    }

    private static string? ReadOptionalString(JsonObject obj, string propertyName, string path, List<CommandTreeError> errors)
    {
        if (!obj.ContainsKey(propertyName))
            return null;

        return ReadString(obj[propertyName], JsonPath.Property(path, propertyName), propertyName, errors);
    }

    private static string? ReadString(JsonNode? node, string path, string propertyName, List<CommandTreeError> errors)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text) && text is not null)
            return text;

        errors.Add(new CommandTreeError(path, $"\"{propertyName}\" must be a string."));
        return null;
    }

    private static bool TryReadBool(JsonNode? node, out bool value)
    {
        value = false;
        return node is JsonValue json && json.TryGetValue<bool>(out value);
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]*-[A-Za-z][A-Za-z0-9]*$")]
    private static partial Regex CmdletName();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9.]*$")]
    private static partial Regex ModuleName();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex ParameterName();
}
