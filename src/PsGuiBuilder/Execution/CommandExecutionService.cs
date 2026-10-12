// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.Execution;
using PsGuiBuilder.PowerShell;

namespace PsGuiBuilder.Execution;

public sealed class CommandExecutionService : ICommandExecutor
{
    public Task<int> RunAsync(
        string script,
        bool elevated,
        bool inProcess,
        IExecutionOutput output,
        CancellationToken cancellationToken)
    {
        if (elevated)
            return new ElevatedCommandRunner().RunAsync(script, output, cancellationToken);

        if (inProcess)
            return new InProcessScriptInvoker().InvokeAsync(script, output, cancellationToken);

        return new ProcessScriptInvoker(PowerShellLocator.Resolve()).InvokeAsync(script, output, cancellationToken);
    }
}
