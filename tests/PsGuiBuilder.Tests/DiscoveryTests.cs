// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.CommandTree;
using PsGuiBuilder.Core.Discovery;

namespace PsGuiBuilder.Tests;

public class DiscoveryTests
{
    [Fact]
    public void Parses_a_catalog_and_filters_it()
    {
        var catalog = DiscoveryCatalog.Parse(
            """
            {
              "modules": { "name": "Microsoft.PowerShell.Management", "version": "7.4.0" },
              "commands": [
                { "name": "Get-ChildItem", "module": "Microsoft.PowerShell.Management", "verb": "Get", "noun": "ChildItem", "commandType": "Cmdlet" },
                { "name": "Set-Item", "module": "Microsoft.PowerShell.Management", "verb": "Set", "noun": "Item", "commandType": "Cmdlet" },
                { "name": "gci", "module": "Microsoft.PowerShell.Management", "verb": "Get", "noun": "ChildItem", "commandType": "Alias" },
                { "name": "Get-ADUser", "module": "ActiveDirectory", "verb": "Get", "noun": "ADUser", "commandType": "Cmdlet" }
              ]
            }
            """,
            PowerShellEdition.PowerShell7);

        Assert.Equal(new[] { "Microsoft.PowerShell.Management" }, catalog.Modules.Select(module => module.Name).ToArray());
        Assert.Equal(4, catalog.Commands.Count);

        var page = DiscoveryBrowser.Search(catalog, new DiscoveryQuery { Text = "child" });
        Assert.Equal(new[] { "Get-ChildItem" }, page.Hits.Select(hit => hit.Command.Name).ToArray());

        var directory = DiscoveryBrowser.Search(catalog, new DiscoveryQuery { Module = "ActiveDirectory" });
        Assert.Equal("Get-ADUser", directory.Hits[0].Command.Name);

        var nouns = DiscoveryBrowser.Nouns(catalog, "Microsoft.PowerShell.Management", includeAliases: false);
        Assert.Equal(new[] { "ChildItem", "Item" }, nouns);

        var withAliases = DiscoveryBrowser.Search(catalog, new DiscoveryQuery { IncludeAliases = true, Verb = "Get", Noun = "ChildItem" });
        Assert.Equal(2, withAliases.TotalMatches);

        var grouped = DiscoveryBrowser.Search(catalog, new DiscoveryQuery { GroupBy = DiscoveryGroupBy.Noun });
        Assert.Equal("ADUser", grouped.Hits[0].Group);
    }

    [Fact]
    public void Cache_round_trips_per_shell_and_ignores_a_corrupt_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "shellmason-discovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var catalog = new DiscoveryCatalog(
                PowerShellEdition.WindowsPowerShell5,
                DateTimeOffset.Parse("2026-10-14T12:00:00Z"),
                new[] { new DiscoveredModule("ActiveDirectory", "1.0") },
                new[] { new DiscoveredCommand("Get-ADUser", "ActiveDirectory", "Get", "ADUser", "Cmdlet") });
            var path = DiscoveryCache.FilePath(root, PowerShellEdition.WindowsPowerShell5);
            DiscoveryCache.Save(path, catalog);

            Assert.True(DiscoveryCache.TryLoad(path, PowerShellEdition.WindowsPowerShell5, out var loaded));
            Assert.NotNull(loaded);
            Assert.Equal("Get-ADUser", loaded!.Commands[0].Name);
            Assert.False(DiscoveryCache.TryLoad(path, PowerShellEdition.PowerShell7, out _));

            File.WriteAllText(path, "{");
            Assert.False(DiscoveryCache.TryLoad(path, PowerShellEdition.WindowsPowerShell5, out _));
            Assert.Contains("discovery-windows-powershell.json", path, StringComparison.Ordinal);
            Assert.Contains("Shellmason", path, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
