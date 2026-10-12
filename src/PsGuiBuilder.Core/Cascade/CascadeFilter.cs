// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

namespace PsGuiBuilder.Core.Cascade;

public static class CascadeFilter
{
    public static IReadOnlyList<CascadeOption> Apply(IReadOnlyList<CascadeOption> options, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return options;

        var tokens = Tokens(filter);
        var matches = new List<CascadeOption>();
        foreach (var option in options)
        {
            if (Matches(option, tokens))
                matches.Add(option);
        }

        return matches;
    }

    public static bool Matches(CascadeOption option, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        return Matches(option, Tokens(filter));
    }

    private static bool Matches(CascadeOption option, string[] tokens)
    {
        var haystack = string.IsNullOrEmpty(option.Description)
            ? option.Name
            : option.Name + " " + option.Description;

        foreach (var token in tokens)
        {
            if (haystack.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        return true;
    }

    private static string[] Tokens(string filter) =>
        filter.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
}
