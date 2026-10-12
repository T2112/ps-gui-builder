// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

namespace PsGuiBuilder.Core.Execution;

public interface IExecutionOutput
{
    void WriteStdout(string text);

    void WriteStderr(string text);
}

public interface IScriptInvoker
{
    Task<int> InvokeAsync(string script, IExecutionOutput output, CancellationToken cancellationToken);
}
