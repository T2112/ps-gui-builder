// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Core.Building;

public interface IModuleInstallGate
{
    Task<ModuleInstallPlan?> FindMissingAsync(string? moduleName, PowerShellEdition edition, CancellationToken cancellationToken);

    Task InstallAsync(ModuleInstallPlan plan, CancellationToken cancellationToken);
}
