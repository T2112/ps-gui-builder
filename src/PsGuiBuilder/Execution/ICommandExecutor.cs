// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.Execution;

namespace PsGuiBuilder.Execution;

public interface ICommandExecutor
{
    Task<int> RunAsync(
        ScriptRequest request,
        bool elevated,
        bool inProcess,
        PowerShellShell? shell,
        IExecutionOutput output,
        CancellationToken cancellationToken);
}
