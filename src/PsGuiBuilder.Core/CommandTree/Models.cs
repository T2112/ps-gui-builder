// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

namespace PsGuiBuilder.Core.CommandTree;

public enum ParameterType
{
    String,
    Choice,
    Switch,
    Bool,
    Int,
    Number,
    SecureString,
    Array
}

public enum PowerShellEdition
{
    PowerShell7,
    WindowsPowerShell5
}

public enum SignInMode
{
    Interactive,
    Credential
}

public sealed class CommandParameter
{
    public CommandParameter(
        string name,
        string label,
        ParameterType type,
        bool required,
        string? placeholder,
        string? defaultValue,
        IReadOnlyList<string> choices)
    {
        Name = name;
        Label = label;
        Type = type;
        Required = required;
        Placeholder = placeholder;
        Default = defaultValue;
        Choices = choices;
    }

    public string Name { get; }

    public string Label { get; }

    public ParameterType Type { get; }

    public bool Required { get; }

    public string? Placeholder { get; }

    public string? Default { get; }

    public IReadOnlyList<string> Choices { get; }
}

public sealed class SignInRequirement
{
    public const string SecretKey = "signin";

    public SignInRequirement(string service, SignInMode mode, string prompt, string command)
    {
        Service = service;
        Mode = mode;
        Prompt = prompt;
        Command = command;
    }

    public string Service { get; }

    public SignInMode Mode { get; }

    public string Prompt { get; }

    public string Command { get; }
}

public sealed class CommandDefinition
{
    public CommandDefinition(
        string name,
        IReadOnlyList<string> path,
        string? description,
        string cmdlet,
        string? module,
        bool requiresAdmin,
        string template,
        string sentence,
        IReadOnlyList<CommandParameter> parameters,
        PowerShellEdition shell = PowerShellEdition.PowerShell7,
        SignInRequirement? signIn = null)
    {
        Name = name;
        Path = path;
        Description = description;
        Cmdlet = cmdlet;
        Module = module;
        RequiresAdmin = requiresAdmin;
        Template = template;
        Sentence = sentence;
        Parameters = parameters;
        Shell = shell;
        SignIn = signIn;
    }

    public string Name { get; }

    public IReadOnlyList<string> Path { get; }

    public string? Description { get; }

    public string Cmdlet { get; }

    public string? Module { get; }

    public bool RequiresAdmin { get; }

    public string Template { get; }

    public string Sentence { get; }

    public IReadOnlyList<CommandParameter> Parameters { get; }

    public PowerShellEdition Shell { get; }

    public SignInRequirement? SignIn { get; }

    public string PathDisplay => string.Join("  →  ", Path);

    public string Summary
    {
        get
        {
            var parts = new List<string> { Cmdlet };
            if (!string.IsNullOrEmpty(Module))
                parts.Add("module " + Module);
            parts.Add(RequiresAdmin ? "administrator" : "current user");
            parts.Add(Shell == PowerShellEdition.WindowsPowerShell5 ? "Windows PowerShell 5.1" : "PowerShell 7");
            if (SignIn is not null)
                parts.Add("sign in to " + SignIn.Service);
            return string.Join("  ·  ", parts);
        }
    }
}

public sealed class CommandNode
{
    public CommandNode(
        string name,
        string? description,
        IReadOnlyList<CommandNode> children,
        CommandDefinition? command)
    {
        Name = name;
        Description = description;
        Children = children;
        Command = command;
    }

    public string Name { get; }

    public string? Description { get; }

    public IReadOnlyList<CommandNode> Children { get; }

    public CommandDefinition? Command { get; }

    public bool IsCommand => Command is not null;
}

public sealed class CommandTree
{
    public CommandTree(string version, string? description, CommandNode root)
    {
        Version = version;
        Description = description;
        Root = root;
    }

    public string Version { get; }

    public string? Description { get; }

    public CommandNode Root { get; }
}
