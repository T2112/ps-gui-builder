// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

namespace PsGuiBuilder.Core.Execution;

public sealed class ExecutionClient
{
    public async Task<int> RunAsync(
        IFrameTransport transport,
        string script,
        IExecutionOutput output,
        CancellationToken cancellationToken)
    {
        if (transport is null)
            throw new ArgumentNullException(nameof(transport));
        if (output is null)
            throw new ArgumentNullException(nameof(output));

        await transport.SendAsync(ExecutionMessage.ScriptMessage(script), cancellationToken).ConfigureAwait(false);
        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                transport.SendAsync(ExecutionMessage.CancelMessage(), CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException or OperationCanceledException)
            {
            }
        });

        while (true)
        {
            ExecutionMessage? message;
            try
            {
                message = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                throw new IOException("The elevated helper closed the connection before reporting an exit code.", ex);
            }

            if (message is null)
                throw new IOException("The elevated helper closed the connection before reporting an exit code.");

            switch (message.Kind)
            {
                case ExecutionKinds.Stdout:
                    output.WriteStdout(message.Text ?? "");
                    break;
                case ExecutionKinds.Stderr:
                    output.WriteStderr(message.Text ?? "");
                    break;
                case ExecutionKinds.Exit:
                    return message.Code ?? 0;
                case ExecutionKinds.Cancelled:
                    throw new OperationCanceledException("The elevated helper cancelled the script.", cancellationToken);
                case ExecutionKinds.Error:
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(message.Message)
                        ? "The elevated helper reported an error."
                        : message.Message);
                default:
                    throw new InvalidDataException($"Unknown execution message \"{message.Kind}\".");
            }
        }
    }
}
