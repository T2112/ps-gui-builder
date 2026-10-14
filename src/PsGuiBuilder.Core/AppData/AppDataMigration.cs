// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Core.AppData;

public static class AppDataMigration
{
    public static IReadOnlyList<string> FilesToCopy(
        string applicationData,
        Func<string, bool> directoryExists,
        Func<string, IEnumerable<string>> listFiles,
        Func<string, bool> fileExists)
    {
        if (directoryExists is null)
            throw new ArgumentNullException(nameof(directoryExists));
        if (listFiles is null)
            throw new ArgumentNullException(nameof(listFiles));
        if (fileExists is null)
            throw new ArgumentNullException(nameof(fileExists));
        if (string.IsNullOrWhiteSpace(applicationData))
            return Array.Empty<string>();

        var legacy = Path.Combine(applicationData, CommandTreeLocations.LegacyUserFolderName);
        if (!directoryExists(legacy))
            return Array.Empty<string>();

        var current = Path.Combine(applicationData, CommandTreeLocations.UserFolderName);
        var planned = new List<string>();
        foreach (var source in listFiles(legacy))
        {
            var name = Path.GetFileName(source);
            if (string.IsNullOrEmpty(name) || !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                continue;

            var destination = Path.Combine(current, name);
            if (fileExists(destination))
                continue;

            planned.Add(source);
        }

        return planned;
    }

    public static string DestinationFor(string applicationData, string sourceFile) =>
        Path.Combine(applicationData, CommandTreeLocations.UserFolderName, Path.GetFileName(sourceFile));
}
