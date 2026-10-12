// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

namespace PsGuiBuilder.Core.Execution;

public sealed class StreamFrameTransport : IFrameTransport, IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private int _disposed;

    public StreamFrameTransport(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public async Task SendAsync(ExecutionMessage message, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        var frame = MessageFramer.Frame(ExecutionMessageCodec.ToUtf8(message));
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<ExecutionMessage?> ReceiveAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        var payload = await MessageFramer.ReadPayloadAsync(_stream, cancellationToken).ConfigureAwait(false);
        if (payload is null)
            return null;

        return ExecutionMessageCodec.FromUtf8(payload);
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return ValueTask.CompletedTask;

        _writeLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
