// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

namespace PsGuiBuilder.Core.Execution;

public sealed class ElevatedHostWorker
{
    public async Task RunAsync(IFrameTransport transport, IScriptInvoker invoker, CancellationToken cancellationToken)
    {
        if (transport is null)
            throw new ArgumentNullException(nameof(transport));
        if (invoker is null)
            throw new ArgumentNullException(nameof(invoker));

        var request = await ReadScriptAsync(transport, cancellationToken).ConfigureAwait(false);
        if (request is null)
            return;

        using var invokeCts = new CancellationTokenSource();
        using var watchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var watch = WatchAsync(transport, invokeCts, watchCts.Token);
        var output = new TransportOutput(transport);
        try
        {
            var code = await invoker.InvokeAsync(request, output, invokeCts.Token).ConfigureAwait(false);
            await TrySendAsync(transport, ExecutionMessage.ExitMessage(code)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await TrySendAsync(transport, ExecutionMessage.CancelledMessage()).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            invokeCts.Cancel();
        }
        catch (Exception ex)
        {
            await TrySendAsync(transport, ExecutionMessage.ErrorMessage(ex.Message)).ConfigureAwait(false);
        }
        finally
        {
            watchCts.Cancel();
            try
            {
                await watch.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static async Task<ScriptRequest?> ReadScriptAsync(IFrameTransport transport, CancellationToken cancellationToken)
    {
        while (true)
        {
            var incoming = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (incoming is null)
                return null;

            if (incoming.Kind == ExecutionKinds.Cancel)
            {
                await TrySendAsync(transport, ExecutionMessage.CancelledMessage()).ConfigureAwait(false);
                return null;
            }

            if (incoming.Kind != ExecutionKinds.Script)
                continue;

            if (string.IsNullOrWhiteSpace(incoming.Script))
            {
                await TrySendAsync(transport, ExecutionMessage.ErrorMessage("The script is empty.")).ConfigureAwait(false);
                return null;
            }

            return new ScriptRequest(incoming.Script, incoming.Secrets, incoming.Interactive == true);
        }
    }

    private static async Task WatchAsync(IFrameTransport transport, CancellationTokenSource invokeCts, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (message is null || message.Kind == ExecutionKinds.Cancel)
                {
                    invokeCts.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidDataException)
        {
            invokeCts.Cancel();
        }
    }

    private static async Task TrySendAsync(IFrameTransport transport, ExecutionMessage message)
    {
        try
        {
            await transport.SendAsync(message, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
    }

    private sealed class TransportOutput : IExecutionOutput
    {
        private readonly IFrameTransport _transport;

        public TransportOutput(IFrameTransport transport) => _transport = transport;

        public void WriteStdout(string text) => Send(ExecutionMessage.StdoutMessage(text));

        public void WriteStderr(string text) => Send(ExecutionMessage.StderrMessage(text));

        private void Send(ExecutionMessage message) =>
            _transport.SendAsync(message, CancellationToken.None).GetAwaiter().GetResult();
    }
}
