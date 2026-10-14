// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

namespace PsGuiBuilder.Core.Updates;

public readonly record struct UpdatePreToken(bool Numeric, int Number, string Text) : IComparable<UpdatePreToken>
{
    public int CompareTo(UpdatePreToken other)
    {
        if (Numeric != other.Numeric)
            return Numeric ? -1 : 1;

        return Numeric
            ? Number.CompareTo(other.Number)
            : string.Compare(Text, other.Text, StringComparison.Ordinal);
    }
}

public sealed class UpdateVersion : IComparable<UpdateVersion>
{
    private UpdateVersion(int[] parts, bool final, UpdatePreToken[] prerelease, string published)
    {
        Parts = parts;
        Final = final;
        Prerelease = prerelease;
        Published = published;
    }

    public IReadOnlyList<int> Parts { get; }

    public bool Final { get; }

    public IReadOnlyList<UpdatePreToken> Prerelease { get; }

    public string Published { get; }

    public static UpdateVersion Parse(string value)
    {
        var published = PublishedText(value);
        if (published.Length == 0)
            throw new FormatException("Version is empty.");

        var main = published;
        var pre = "";
        var dash = published.IndexOf('-');
        if (dash >= 0)
        {
            main = published[..dash];
            pre = published[(dash + 1)..];
        }

        if (main.Length == 0)
            throw new FormatException($"Not a version: {value}");

        var numbers = new List<int>();
        foreach (var piece in main.Split('.'))
        {
            if (piece.Length == 0 || piece.Any(character => character is < '0' or > '9'))
                throw new FormatException($"Not a version: {value}");

            numbers.Add(int.Parse(piece, System.Globalization.CultureInfo.InvariantCulture));
        }

        while (numbers.Count > 1 && numbers[^1] == 0)
            numbers.RemoveAt(numbers.Count - 1);

        var tokens = new List<UpdatePreToken>();
        if (pre.Length > 0)
        {
            foreach (var piece in pre.Split('.'))
            {
                if (piece.Length == 0)
                    continue;

                tokens.Add(piece.All(character => character is >= '0' and <= '9')
                    ? new UpdatePreToken(true, int.Parse(piece, System.Globalization.CultureInfo.InvariantCulture), "")
                    : new UpdatePreToken(false, 0, piece));
            }
        }

        return new UpdateVersion(numbers.ToArray(), tokens.Count == 0, tokens.ToArray(), published);
    }

    public static bool TryParse(string? value, out UpdateVersion version)
    {
        version = null!;
        try
        {
            version = Parse(value ?? "");
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string PublishedText(string? value)
    {
        var text = (value ?? "").Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
            text = text[1..].Trim();

        var plus = text.IndexOf('+');
        if (plus >= 0)
            text = text[..plus];

        return text;
    }

    public int CompareTo(UpdateVersion? other)
    {
        if (other is null)
            return 1;

        var length = Math.Max(Parts.Count, other.Parts.Count);
        for (var index = 0; index < length; index++)
        {
            var left = index < Parts.Count ? Parts[index] : 0;
            var right = index < other.Parts.Count ? other.Parts[index] : 0;
            var compared = left.CompareTo(right);
            if (compared != 0)
                return compared;
        }

        var final = Final.CompareTo(other.Final);
        if (final != 0)
            return final;

        var preLength = Math.Max(Prerelease.Count, other.Prerelease.Count);
        for (var index = 0; index < preLength; index++)
        {
            if (index >= Prerelease.Count)
                return -1;
            if (index >= other.Prerelease.Count)
                return 1;

            var compared = Prerelease[index].CompareTo(other.Prerelease[index]);
            if (compared != 0)
                return compared;
        }

        return 0;
    }

    public static bool operator >(UpdateVersion left, UpdateVersion right) => left.CompareTo(right) > 0;

    public static bool operator <(UpdateVersion left, UpdateVersion right) => left.CompareTo(right) < 0;
}
