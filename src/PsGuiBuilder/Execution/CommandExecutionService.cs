// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.Execution;
using PsGuiBuilder.PowerShell;

namespace PsGuiBuilder.Execution;

public sealed class CommandExecutionService : ICommandExecutor
{
    public Task<int> RunAsync(
        ScriptRequest request,
        bool elevated,
        bool inProcess,
        PowerShellShell? shell,
        IExecutionOutput output,
        CancellationToken cancellationToken)
    {
        if (elevated)
            return new ElevatedCommandRunner().RunAsync(request, output, cancellationToken);

        if (inProcess)
            return new InProcessScriptInvoker().InvokeAsync(request, output, cancellationToken);

        if (shell is null)
            throw new InvalidOperationException("No PowerShell executable is available.");

        return new ProcessScriptInvoker(shell).InvokeAsync(request, output, cancellationToken);
    }
}
