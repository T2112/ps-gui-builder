// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text.Json;
using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Core.Discovery;

public static class DiscoveryCache
{
    public static string FileName(PowerShellEdition edition) =>
        edition == PowerShellEdition.WindowsPowerShell5
            ? "discovery-windows-powershell.json"
            : "discovery-powershell7.json";

    public static string FilePath(string applicationData, PowerShellEdition edition) =>
        Path.Combine(applicationData, CommandTreeLocations.UserFolderName, FileName(edition));

    public static bool TryLoad(string path, PowerShellEdition edition, out DiscoveryCatalog? catalog)
    {
        catalog = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("edition", out var editionProperty)
                || !Enum.TryParse(editionProperty.GetString(), out PowerShellEdition stored)
                || stored != edition)
            {
                return false;
            }

            var captured = DateTimeOffset.UtcNow;
            if (root.TryGetProperty("capturedUtc", out var capturedProperty)
                && DateTimeOffset.TryParse(capturedProperty.GetString(), out var parsed))
            {
                captured = parsed;
            }

            catalog = DiscoveryCatalog.Parse(root.GetRawText(), edition, captured);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or IOException)
        {
            return false;
        }
    }

    public static void Save(string path, DiscoveryCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A cache path is required.", nameof(path));
        if (catalog is null)
            throw new ArgumentNullException(nameof(catalog));

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var payload = new
        {
            edition = catalog.Edition.ToString(),
            capturedUtc = catalog.CapturedUtc.ToString("O"),
            modules = catalog.Modules.Select(module => new { name = module.Name, version = module.Version }),
            commands = catalog.Commands.Select(command => new
            {
                name = command.Name,
                module = command.Module,
                verb = command.Verb,
                noun = command.Noun,
                commandType = command.CommandType
            })
        };
        File.WriteAllText(path, JsonSerializer.Serialize(payload));
    }
}
