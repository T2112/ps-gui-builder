// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.Cascade;

namespace PsGuiBuilder.Core.Discovery;

public enum DiscoveryGroupBy
{
    Module,
    Noun
}

public sealed class DiscoveryQuery
{
    public string? Text { get; init; }

    public string? Module { get; init; }

    public string? Noun { get; init; }

    public string? Verb { get; init; }

    public bool IncludeAliases { get; init; }

    public DiscoveryGroupBy GroupBy { get; init; } = DiscoveryGroupBy.Module;
}

public sealed class DiscoveryHit
{
    public DiscoveryHit(string group, DiscoveredCommand command)
    {
        Group = group;
        Command = command;
    }

    public string Group { get; }

    public DiscoveredCommand Command { get; }
}

public sealed class DiscoveryPage
{
    public DiscoveryPage(IReadOnlyList<DiscoveryHit> hits, int totalMatches)
    {
        Hits = hits;
        TotalMatches = totalMatches;
    }

    public const int ResultLimit = 400;

    public IReadOnlyList<DiscoveryHit> Hits { get; }

    public int TotalMatches { get; }

    public bool Truncated => TotalMatches > Hits.Count;
}

public static class DiscoveryBrowser
{
    public static DiscoveryPage Search(DiscoveryCatalog catalog, DiscoveryQuery query)
    {
        if (catalog is null)
            throw new ArgumentNullException(nameof(catalog));
        if (query is null)
            throw new ArgumentNullException(nameof(query));

        var matches = new List<DiscoveredCommand>();
        foreach (var command in catalog.Commands)
        {
            if (!Include(command, query.IncludeAliases))
                continue;
            if (!EqualsFilter(command.Module, query.Module))
                continue;
            if (!EqualsFilter(command.Noun, query.Noun))
                continue;
            if (!EqualsFilter(command.Verb, query.Verb))
                continue;
            if (!string.IsNullOrWhiteSpace(query.Text))
            {
                var option = new CascadeOption(
                    command.Name,
                    command.Module + " " + command.Verb + " " + command.Noun,
                    isCommand: true);
                if (!CascadeFilter.Matches(option, query.Text))
                    continue;
            }

            matches.Add(command);
        }

        matches.Sort((left, right) =>
        {
            var group = string.Compare(GroupOf(left, query.GroupBy), GroupOf(right, query.GroupBy), StringComparison.OrdinalIgnoreCase);
            return group != 0
                ? group
                : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        });

        var hits = new DiscoveryHit[Math.Min(matches.Count, DiscoveryPage.ResultLimit)];
        for (var index = 0; index < hits.Length; index++)
            hits[index] = new DiscoveryHit(GroupOf(matches[index], query.GroupBy), matches[index]);

        return new DiscoveryPage(hits, matches.Count);
    }

    public static IReadOnlyList<string> Modules(DiscoveryCatalog catalog, bool includeAliases)
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in catalog.Modules)
        {
            if (module.Name.Length > 0)
                names.Add(module.Name);
        }

        foreach (var command in catalog.Commands)
        {
            if (Include(command, includeAliases) && command.Module.Length > 0)
                names.Add(command.Module);
        }

        return names.ToArray();
    }

    public static IReadOnlyList<string> Nouns(DiscoveryCatalog catalog, string? module, bool includeAliases) =>
        Distinct(catalog, includeAliases, module, noun: null, command => command.Noun);

    public static IReadOnlyList<string> Verbs(DiscoveryCatalog catalog, string? module, string? noun, bool includeAliases) =>
        Distinct(catalog, includeAliases, module, noun, command => command.Verb);

    private static IReadOnlyList<string> Distinct(
        DiscoveryCatalog catalog,
        bool includeAliases,
        string? module,
        string? noun,
        Func<DiscoveredCommand, string> select)
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var command in catalog.Commands)
        {
            if (!Include(command, includeAliases))
                continue;
            if (!EqualsFilter(command.Module, module))
                continue;
            if (!EqualsFilter(command.Noun, noun))
                continue;

            var value = select(command);
            if (value.Length > 0)
                names.Add(value);
        }

        return names.ToArray();
    }

    private static bool Include(DiscoveredCommand command, bool includeAliases) =>
        includeAliases || !command.CommandType.Equals("Alias", StringComparison.OrdinalIgnoreCase);

    private static bool EqualsFilter(string value, string? filter) =>
        string.IsNullOrWhiteSpace(filter) || value.Equals(filter.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string GroupOf(DiscoveredCommand command, DiscoveryGroupBy groupBy)
    {
        if (groupBy == DiscoveryGroupBy.Noun)
            return command.Noun.Length == 0 ? "(no noun)" : command.Noun;

        return command.Module.Length == 0 ? "(no module)" : command.Module;
    }
}
