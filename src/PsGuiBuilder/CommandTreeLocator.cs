// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.IO;
using PsGuiBuilder.Core.AppData;
using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder;

public static class CommandTreeLocator
{
    public static string Find(IReadOnlyList<string> args)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(appData))
        {
            foreach (var source in AppDataMigration.FilesToCopy(appData, Directory.Exists, Directory.EnumerateFiles, File.Exists))
                CopyFile(source, AppDataMigration.DestinationFor(appData, source));
        }

        return CommandTreeLocations.Resolve(
            args,
            appData,
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
            File.Exists,
            SeedUserCopy);
    }

    private static void SeedUserCopy(string source, string destination) =>
        CopyFile(source, destination);

    private static void CopyFile(string source, string destination)
    {
        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.Copy(source, destination, overwrite: false);
    }
}
