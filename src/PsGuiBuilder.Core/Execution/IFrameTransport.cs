// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

namespace PsGuiBuilder.Core.Execution;

public interface IFrameTransport
{
    Task SendAsync(ExecutionMessage message, CancellationToken cancellationToken);

    Task<ExecutionMessage?> ReceiveAsync(CancellationToken cancellationToken);
}
