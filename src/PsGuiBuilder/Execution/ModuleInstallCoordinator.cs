// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.Building;
using PsGuiBuilder.Core.CommandTree;
using PsGuiBuilder.Core.Execution;

namespace PsGuiBuilder.Execution;

public sealed class ModuleInstallCoordinator : IModuleInstallGate
{
    public async Task<ModuleInstallPlan?> FindMissingAsync(
        string? moduleName,
        PowerShellEdition edition,
        CancellationToken cancellationToken)
    {
        if (!ModuleInstallPlanner.TryCreate(moduleName, edition, out var plan) || plan is null)
            return null;

        var resolution = PowerShellLocator.Resolve(edition);
        if (resolution.Error is not null || resolution.Shell is null)
            throw new InvalidOperationException(resolution.Error ?? "PowerShell was not found.");

        var output = new TextCapture();
        var code = await new ProcessScriptInvoker(resolution.Shell)
            .InvokeAsync(ScriptRequest.Plain(plan.PresenceScript), output, cancellationToken)
            .ConfigureAwait(false);
        if (code != 0)
        {
            var detail = string.IsNullOrWhiteSpace(output.ErrorText) ? output.OutputText : output.ErrorText;
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
                ? "Could not check whether " + plan.ModuleName + " is installed."
                : detail.Trim());
        }

        var text = output.OutputText;
        if (text.Contains("missing", StringComparison.OrdinalIgnoreCase))
            return plan;
        if (text.Contains("installed", StringComparison.OrdinalIgnoreCase))
            return null;

        throw new InvalidOperationException("Could not tell whether " + plan.ModuleName + " is installed.");
    }

    public async Task InstallAsync(ModuleInstallPlan plan, CancellationToken cancellationToken)
    {
        if (plan is null)
            throw new ArgumentNullException(nameof(plan));

        var request = ScriptRequest.Plain(plan.Script);
        var output = new TextCapture();
        int code;
        if (plan.RequiresAdmin)
        {
            await using var connection = await new ElevatedCommandRunner().ConnectAsync(cancellationToken).ConfigureAwait(false);
            code = await connection.InvokeAsync(request, output, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var resolution = PowerShellLocator.Resolve(plan.Edition);
            if (resolution.Error is not null || resolution.Shell is null)
                throw new InvalidOperationException(resolution.Error ?? "PowerShell was not found.");

            code = await new ProcessScriptInvoker(resolution.Shell)
                .InvokeAsync(request, output, cancellationToken)
                .ConfigureAwait(false);
        }

        if (code != 0)
        {
            var detail = string.IsNullOrWhiteSpace(output.ErrorText) ? output.OutputText : output.ErrorText;
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
                ? "Installing " + plan.ModuleName + " failed with exit code " + code + "."
                : detail.Trim());
        }
    }
}
