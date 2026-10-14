// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

namespace PsGuiBuilder.Core.CommandTree;

public static class CommandTreeLocations
{
    public const string FileName = "command-tree.json";
    public const string UserFolderName = "Shellmason";
    public const string LegacyUserFolderName = "PsGuiBuilder";
    public const string InstalledMarkerName = "PsGuiBuilder.installed";

    public static string UserFile(string applicationData) =>
        Path.Combine(applicationData, UserFolderName, FileName);

    public static string Resolve(
        IReadOnlyList<string> args,
        string? applicationData,
        string baseDirectory,
        string currentDirectory,
        Func<string, bool> exists,
        Action<string, string>? copy = null)
    {
        if (args is null)
            throw new ArgumentNullException(nameof(args));
        if (exists is null)
            throw new ArgumentNullException(nameof(exists));
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("The application directory is required.", nameof(baseDirectory));

        var requested = ExplicitPath(args);
        if (requested is not null)
        {
            var full = Path.GetFullPath(requested);
            if (!exists(full))
                throw new FileNotFoundException("The command tree file was not found.", full);

            return full;
        }

        var userFile = string.IsNullOrWhiteSpace(applicationData) ? null : UserFile(applicationData);
        if (userFile is not null && exists(userFile))
            return userFile;

        var defaultFile = Path.Combine(baseDirectory, FileName);
        var marker = Path.Combine(baseDirectory, InstalledMarkerName);
        if (userFile is not null && exists(marker) && exists(defaultFile) && !exists(userFile))
        {
            try
            {
                copy?.Invoke(defaultFile, userFile);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            if (exists(userFile))
                return userFile;
        }

        if (exists(defaultFile))
            return defaultFile;

        if (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            var current = Path.Combine(currentDirectory, FileName);
            if (exists(current))
                return current;
        }

        var dir = Parent(baseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, FileName);
            if (exists(candidate))
                return candidate;

            dir = Parent(dir);
        }

        throw new FileNotFoundException(
            "command-tree.json was not found. Pass --tree <path>, or place the file in %APPDATA%\\" + UserFolderName + "\\.");
    }

    private static string? ExplicitPath(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count; index++)
        {
            if (!string.Equals(args[index], "--tree", StringComparison.OrdinalIgnoreCase))
                continue;

            if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]))
                throw new ArgumentException("Pass a file path after --tree.");

            return args[index + 1];
        }

        return null;
    }

    private static string? Parent(string path)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(path);
        var parent = Path.GetDirectoryName(trimmed);
        if (string.IsNullOrEmpty(parent) || string.Equals(parent, trimmed, StringComparison.OrdinalIgnoreCase))
            return null;

        return parent;
    }
}
