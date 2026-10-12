// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

namespace PsGuiBuilder.Core.CommandTree;

public sealed record CommandTreeError(string Path, string Message);

public sealed class CommandTreeException : Exception
{
    public CommandTreeException(IReadOnlyList<CommandTreeError> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<CommandTreeError> Errors { get; }

    private static string BuildMessage(IReadOnlyList<CommandTreeError> errors)
    {
        if (errors.Count == 0)
            return "The command tree is invalid.";

        return string.Join(Environment.NewLine, errors.Select(error => $"{error.Path}: {error.Message}"));
    }
}
