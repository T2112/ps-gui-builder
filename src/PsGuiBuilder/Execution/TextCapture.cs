// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text;
using PsGuiBuilder.Core.Execution;

namespace PsGuiBuilder.Execution;

internal sealed class TextCapture : IExecutionOutput
{
    private readonly StringBuilder _stdout = new();
    private readonly StringBuilder _stderr = new();

    public string OutputText => _stdout.ToString();

    public string ErrorText => _stderr.ToString();

    public void WriteStdout(string text) => _stdout.Append(text);

    public void WriteStderr(string text) => _stderr.Append(text);
}
