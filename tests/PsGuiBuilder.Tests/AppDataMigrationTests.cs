// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.AppData;
using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Tests;

public class AppDataMigrationTests
{
    [Fact]
    public void Copies_json_from_the_old_folder_without_replacing_a_newer_file()
    {
        Assert.Equal("Shellmason", CommandTreeLocations.UserFolderName);
        Assert.Equal("PsGuiBuilder", CommandTreeLocations.LegacyUserFolderName);

        var root = Path.Combine(Path.GetTempPath(), "shellmason-migrate-" + Guid.NewGuid().ToString("N"));
        var legacy = Path.Combine(root, "PsGuiBuilder");
        var current = Path.Combine(root, "Shellmason");
        Directory.CreateDirectory(legacy);
        Directory.CreateDirectory(current);
        File.WriteAllText(Path.Combine(legacy, "command-tree.json"), "legacy");
        File.WriteAllText(Path.Combine(legacy, "notes.json"), "notes");
        File.WriteAllText(Path.Combine(legacy, "readme.txt"), "skip");
        File.WriteAllText(Path.Combine(current, "notes.json"), "kept");

        var planned = AppDataMigration.FilesToCopy(root, Directory.Exists, Directory.EnumerateFiles, File.Exists);
        Assert.Equal(new[] { Path.Combine(legacy, "command-tree.json") }, planned);

        foreach (var source in planned)
            File.Copy(source, AppDataMigration.DestinationFor(root, source));

        Assert.Equal("legacy", File.ReadAllText(Path.Combine(current, "command-tree.json")));
        Assert.Equal("kept", File.ReadAllText(Path.Combine(current, "notes.json")));
        Assert.False(File.Exists(Path.Combine(current, "readme.txt")));
        Assert.Equal("legacy", File.ReadAllText(Path.Combine(legacy, "command-tree.json")));
    }
}
