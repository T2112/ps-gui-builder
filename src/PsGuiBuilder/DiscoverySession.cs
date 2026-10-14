// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.CommandTree;
using PsGuiBuilder.Core.Discovery;
using PsGuiBuilder.Core.Execution;
using PsGuiBuilder.Execution;

namespace PsGuiBuilder;

public sealed class DiscoveryLoad
{
    public DiscoveryLoad(DiscoveryCatalog catalog, bool fromCache, string? notice)
    {
        Catalog = catalog;
        FromCache = fromCache;
        Notice = notice;
    }

    public DiscoveryCatalog Catalog { get; }

    public bool FromCache { get; }

    public string? Notice { get; }
}

public interface IDiscoverySource
{
    Task<DiscoveryLoad> LoadAsync(PowerShellEdition edition, bool refresh, CancellationToken cancellationToken);
}

public sealed class DiscoverySession : IDiscoverySource
{
    public Task<DiscoveryLoad> LoadAsync(PowerShellEdition edition, bool refresh, CancellationToken cancellationToken)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var path = string.IsNullOrEmpty(appData) ? null : DiscoveryCache.FilePath(appData, edition);
        if (!refresh && path is not null && DiscoveryCache.TryLoad(path, edition, out var cached) && cached is not null)
            return Task.FromResult(new DiscoveryLoad(cached, fromCache: true, notice: null));

        return ReadMachineAsync(edition, path, cancellationToken);
    }

    private static async Task<DiscoveryLoad> ReadMachineAsync(
        PowerShellEdition edition,
        string? path,
        CancellationToken cancellationToken)
    {
        var resolution = PowerShellLocator.Resolve(edition);
        if (resolution.Error is not null || resolution.Shell is null)
            throw new InvalidOperationException(resolution.Error ?? "PowerShell was not found.");

        var output = new TextCapture();
        var code = await new ProcessScriptInvoker(resolution.Shell)
            .InvokeAsync(ScriptRequest.Plain(DiscoveryScripts.Catalog), output, cancellationToken)
            .ConfigureAwait(false);
        if (code != 0)
        {
            var detail = string.IsNullOrWhiteSpace(output.ErrorText) ? output.OutputText : output.ErrorText;
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail) ? "Discovery failed." : detail.Trim());
        }

        var catalog = DiscoveryCatalog.Parse(output.OutputText, edition);
        if (!string.IsNullOrEmpty(path))
            DiscoveryCache.Save(path, catalog);

        return new DiscoveryLoad(catalog, fromCache: false, resolution.Notice);
    }
}
