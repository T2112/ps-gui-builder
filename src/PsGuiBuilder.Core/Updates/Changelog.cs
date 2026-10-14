// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text.RegularExpressions;

namespace PsGuiBuilder.Core.Updates;

public static partial class Changelog
{
    public const string OwnershipNote =
        "Shellmason. Personal project by Thomas Schueler. Developed on personal time using personal equipment. Not related to any employment.";

    public static string? Section(string? markdown, string? version)
    {
        if (string.IsNullOrWhiteSpace(markdown) || !UpdateVersion.TryParse(version, out var wanted))
            return null;

        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var start = -1;
        for (var index = 0; index < lines.Length; index++)
        {
            if (TryHeading(lines[index], out var heading) && heading.CompareTo(wanted) == 0)
            {
                start = index;
                break;
            }
        }

        if (start < 0)
            return null;

        var end = lines.Length;
        for (var index = start + 1; index < lines.Length; index++)
        {
            if (TryHeading(lines[index], out _))
            {
                end = index;
                break;
            }
        }

        var text = string.Join('\n', lines.Skip(start).Take(end - start)).Trim();
        return text.Length == 0 ? null : text;
    }

    public static string? ReleaseNotes(string? markdown, string? version)
    {
        var section = Section(markdown, version);
        if (string.IsNullOrWhiteSpace(section))
            return null;

        return section.TrimEnd() + "\n\n" + OwnershipNote;
    }

    public static string ForPrompt(string? notes, string? version)
    {
        var section = Section(notes, version);
        var text = string.IsNullOrWhiteSpace(section) ? notes : section;
        return string.IsNullOrWhiteSpace(text) ? "No release notes were published." : text.Trim();
    }

    private static bool TryHeading(string line, out UpdateVersion version)
    {
        version = null!;
        var match = HeadingPattern().Match(line);
        if (!match.Success)
            return false;

        return UpdateVersion.TryParse(match.Groups["version"].Value, out version);
    }

    [GeneratedRegex(@"^##[ \t]+\[?v?(?<version>\d+(?:\.\d+)*)\]?(?:[ \t].*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingPattern();
}
