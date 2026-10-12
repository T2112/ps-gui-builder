// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Core.Cascade;

public sealed class CascadeOption
{
    public CascadeOption(string name, string? description, bool isCommand)
    {
        Name = name;
        Description = description;
        IsCommand = isCommand;
    }

    public string Name { get; }

    public string? Description { get; }

    public bool IsCommand { get; }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
}

public sealed class CascadeLevel
{
    internal CascadeLevel(string prompt, IReadOnlyList<CascadeOption> options, CommandNode parent)
    {
        Prompt = prompt;
        Options = options;
        Parent = parent;
    }

    public string Prompt { get; }

    public IReadOnlyList<CascadeOption> Options { get; }

    public string FilterText { get; internal set; } = "";

    public string? SelectedKey { get; internal set; }

    internal CommandNode Parent { get; }

    public IReadOnlyList<CascadeOption> FilteredOptions => CascadeFilter.Apply(Options, FilterText);
}

public sealed class CascadeSession
{
    private readonly List<CascadeLevel> _levels = new();

    public CascadeSession(PsGuiBuilder.Core.CommandTree.CommandTree tree)
    {
        Tree = tree ?? throw new ArgumentNullException(nameof(tree));
        _levels.Add(CreateLevel(tree.Root, 0));
    }

    public PsGuiBuilder.Core.CommandTree.CommandTree Tree { get; }

    public IReadOnlyList<CascadeLevel> Levels => _levels;

    public CommandDefinition? SelectedCommand { get; private set; }

    public void SetFilter(int levelIndex, string? filterText)
    {
        var level = RequireLevel(levelIndex);
        level.FilterText = filterText ?? "";
    }

    public void Select(int levelIndex, string optionName)
    {
        if (string.IsNullOrWhiteSpace(optionName))
            throw new ArgumentException("An option name is required.", nameof(optionName));

        var level = RequireLevel(levelIndex);
        var child = level.Parent.Children.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, optionName, StringComparison.Ordinal));
        if (child is null)
            throw new ArgumentException($"'{optionName}' is not an option at level {levelIndex}.", nameof(optionName));

        if (string.Equals(level.SelectedKey, optionName, StringComparison.Ordinal))
            return;

        level.SelectedKey = optionName;
        level.FilterText = optionName;
        TruncateAfter(levelIndex);

        if (child.Command is not null)
        {
            SelectedCommand = child.Command;
            return;
        }

        SelectedCommand = null;
        _levels.Add(CreateLevel(child, levelIndex + 1));
    }

    public void ClearSelection(int levelIndex)
    {
        var level = RequireLevel(levelIndex);
        level.SelectedKey = null;
        TruncateAfter(levelIndex);
        SelectedCommand = null;
    }

    public string? TryResolve(int levelIndex, string? text)
    {
        var level = RequireLevel(levelIndex);
        if (string.IsNullOrWhiteSpace(text))
        {
            return level.Options.Count == 1 ? level.Options[0].Name : null;
        }

        var trimmed = text.Trim();
        var exact = level.Options
            .Where(option => string.Equals(option.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (exact.Count == 1)
            return exact[0].Name;

        var filtered = CascadeFilter.Apply(level.Options, trimmed);
        return filtered.Count == 1 ? filtered[0].Name : null;
    }

    public void Reset()
    {
        _levels.Clear();
        _levels.Add(CreateLevel(Tree.Root, 0));
        SelectedCommand = null;
    }

    private CascadeLevel RequireLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= _levels.Count)
            throw new ArgumentOutOfRangeException(nameof(levelIndex), "That cascade level is not open.");

        return _levels[levelIndex];
    }

    private void TruncateAfter(int levelIndex)
    {
        if (_levels.Count > levelIndex + 1)
            _levels.RemoveRange(levelIndex + 1, _levels.Count - levelIndex - 1);
    }

    private static CascadeLevel CreateLevel(CommandNode parent, int index)
    {
        var options = parent.Children
            .Select(child => new CascadeOption(child.Name, Describe(child), child.IsCommand))
            .ToArray();
        var prompt = index == 0 ? "I Want To:" : "Then:";
        return new CascadeLevel(prompt, options, parent);
    }

    private static string? Describe(CommandNode node)
    {
        if (node.Command is null)
            return node.Description;

        var detail = node.Command.Cmdlet;
        if (!string.IsNullOrEmpty(node.Command.Module))
            detail += " · " + node.Command.Module;

        if (string.IsNullOrWhiteSpace(node.Description))
            return detail;

        return node.Description + " · " + detail;
    }
}
