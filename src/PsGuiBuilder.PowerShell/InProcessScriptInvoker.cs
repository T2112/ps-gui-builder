// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Management.Automation;
using PsGuiBuilder.Core.Execution;

namespace PsGuiBuilder.PowerShell;

public sealed class InProcessScriptInvoker : IScriptInvoker
{
    public Task<int> InvokeAsync(string script, IExecutionOutput output, CancellationToken cancellationToken)
    {
        if (output is null)
            throw new ArgumentNullException(nameof(output));

        return Task.Run(() => Invoke(script, output, cancellationToken), cancellationToken);
    }

    private static int Invoke(string script, IExecutionOutput output, CancellationToken cancellationToken)
    {
        using var powerShell = System.Management.Automation.PowerShell.Create();
        powerShell.AddScript(script);
        using var results = new PSDataCollection<PSObject>();
        results.DataAdded += (_, args) =>
        {
            var item = results[args.Index];
            output.WriteStdout((item?.BaseObject?.ToString() ?? string.Empty) + Environment.NewLine);
        };

        var invocation = powerShell;
        powerShell.Streams.Error.DataAdded += (_, args) =>
            output.WriteStderr(invocation.Streams.Error[args.Index] + Environment.NewLine);
        powerShell.Streams.Warning.DataAdded += (_, args) =>
            output.WriteStderr("WARNING: " + invocation.Streams.Warning[args.Index] + Environment.NewLine);
        powerShell.Streams.Information.DataAdded += (_, args) =>
        {
            var record = invocation.Streams.Information[args.Index];
            output.WriteStdout((record?.MessageData?.ToString() ?? string.Empty) + Environment.NewLine);
        };

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                powerShell.Stop();
            }
            catch (InvalidOperationException)
            {
            }
        });

        try
        {
            var asyncResult = powerShell.BeginInvoke<PSObject, PSObject>(new PSDataCollection<PSObject>(), results);
            powerShell.EndInvoke(asyncResult);
            return powerShell.HadErrors ? 1 : 0;
        }
        catch (PipelineStoppedException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("In-process PowerShell was cancelled.", cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("In-process PowerShell was cancelled.", ex, cancellationToken);

            output.WriteStderr(ex.Message + Environment.NewLine);
            return 1;
        }
    }
}
