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
        var ranked = new List<Ranked>(options.Count);
        for (var index = 0; index < options.Count; index++)
        {
            if (TryScore(options[index], tokens, out var score))
                ranked.Add(new Ranked(options[index], score, index));
        }

        ranked.Sort(static (left, right) =>
        {
            var byScore = right.Score.CompareTo(left.Score);
            return byScore != 0 ? byScore : left.Index.CompareTo(right.Index);
        });

        var matches = new CascadeOption[ranked.Count];
        for (var index = 0; index < ranked.Count; index++)
            matches[index] = ranked[index].Option;

        return matches;
    }

    public static bool Matches(CascadeOption option, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        return TryScore(option, Tokens(filter), out _);
    }

    private static bool TryScore(CascadeOption option, string[] tokens, out int score)
    {
        score = 0;
        foreach (var token in tokens)
        {
            var best = Math.Max(ScoreField(option.Name, token, isName: true), ScoreField(option.Description ?? "", token, isName: false));
            if (best <= 0)
                return false;

            score += best;
        }

        if (tokens.Length == 1 && option.Name.Equals(tokens[0], StringComparison.OrdinalIgnoreCase))
            score += 400;

        return true;
    }

    private static int ScoreField(string field, string token, bool isName)
    {
        if (field.Length == 0 || token.Length == 0)
            return 0;

        var index = field.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var score = 220;
            if (index == 0 || (index > 0 && !char.IsLetterOrDigit(field[index - 1])))
                score += 80;

            if (isName)
                score += 70;

            return score;
        }

        var best = 0;
        var start = 0;
        for (var i = 0; i <= field.Length; i++)
        {
            var boundary = i == field.Length || !char.IsLetterOrDigit(field[i]);
            if (!boundary)
                continue;

            if (i > start)
            {
                var word = field[start..i];
                best = Math.Max(best, ScoreFuzzy(word, token, isName));
            }

            start = i + 1;
        }

        return best;
    }

    private static int ScoreFuzzy(string word, string token, bool isName)
    {
        var allowance = token.Length <= 2 ? 0 : token.Length <= 6 ? 1 : 2;
        if (allowance == 0)
            return 0;

        var distance = BestDistance(word, token, allowance);
        if (distance < 0)
            return 0;

        var score = 140 - (distance * 45);
        if (isName)
            score += 25;

        return score;
    }

    private static int BestDistance(string word, string token, int max)
    {
        var best = int.MaxValue;
        var lower = Math.Max(1, token.Length - max);
        var upper = Math.Min(word.Length, token.Length + max);
        if (word.Length < lower)
            return -1;

        for (var length = lower; length <= upper; length++)
        {
            for (var start = 0; start + length <= word.Length; start++)
            {
                var distance = Damerau(word.AsSpan(start, length), token, max);
                if (distance >= 0 && distance < best)
                    best = distance;
            }
        }

        return best == int.MaxValue ? -1 : best;
    }

    private static int Damerau(ReadOnlySpan<char> left, string right, int max)
    {
        if (Math.Abs(left.Length - right.Length) > max)
            return -1;

        var rows = left.Length + 1;
        var columns = right.Length + 1;
        var matrix = new int[rows, columns];
        for (var row = 0; row < rows; row++)
            matrix[row, 0] = row;
        for (var column = 0; column < columns; column++)
            matrix[0, column] = column;

        for (var row = 1; row < rows; row++)
        {
            var rowBest = int.MaxValue;
            for (var column = 1; column < columns; column++)
            {
                var cost = char.ToUpperInvariant(left[row - 1]) == char.ToUpperInvariant(right[column - 1]) ? 0 : 1;
                var value = Math.Min(
                    Math.Min(matrix[row - 1, column] + 1, matrix[row, column - 1] + 1),
                    matrix[row - 1, column - 1] + cost);
                if (row > 1 && column > 1
                    && char.ToUpperInvariant(left[row - 1]) == char.ToUpperInvariant(right[column - 2])
                    && char.ToUpperInvariant(left[row - 2]) == char.ToUpperInvariant(right[column - 1]))
                {
                    value = Math.Min(value, matrix[row - 2, column - 2] + 1);
                }

                matrix[row, column] = value;
                if (value < rowBest)
                    rowBest = value;
            }

            if (rowBest > max)
                return -1;
        }

        var distance = matrix[left.Length, right.Length];
        return distance <= max ? distance : -1;
    }

    private static string[] Tokens(string filter) =>
        filter.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private readonly record struct Ranked(CascadeOption Option, int Score, int Index);
}
