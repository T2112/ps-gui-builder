// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text;
using System.Threading.Channels;
using PsGuiBuilder.Core.Execution;

namespace PsGuiBuilder.Tests.Support;

internal static class Repo
{
    public static string CommandTreePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "command-tree.json");
            var ownership = Path.Combine(dir.FullName, "OWNERSHIP.md");
            if (File.Exists(candidate) && File.Exists(ownership))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException("command-tree.json was not found from the test output directory.");
    }
}

internal sealed class CollectingOutput : IExecutionOutput
{
    private readonly StringBuilder _stdout = new();
    private readonly StringBuilder _stderr = new();
    private readonly object _gate = new();

    public string Stdout
    {
        get
        {
            lock (_gate)
                return _stdout.ToString();
        }
    }

    public string Stderr
    {
        get
        {
            lock (_gate)
                return _stderr.ToString();
        }
    }

    public void WriteStdout(string text)
    {
        lock (_gate)
            _stdout.Append(text);
    }

    public void WriteStderr(string text)
    {
        lock (_gate)
            _stderr.Append(text);
    }
}

internal sealed class ChannelFrameTransport : IFrameTransport
{
    private readonly ChannelWriter<ExecutionMessage> _writer;
    private readonly ChannelReader<ExecutionMessage> _reader;

    private ChannelFrameTransport(ChannelWriter<ExecutionMessage> writer, ChannelReader<ExecutionMessage> reader)
    {
        _writer = writer;
        _reader = reader;
    }

    public static (ChannelFrameTransport Parent, ChannelFrameTransport Child) CreatePair()
    {
        var toChild = Channel.CreateUnbounded<ExecutionMessage>();
        var toParent = Channel.CreateUnbounded<ExecutionMessage>();
        return (
            new ChannelFrameTransport(toChild.Writer, toParent.Reader),
            new ChannelFrameTransport(toParent.Writer, toChild.Reader));
    }

    public async Task SendAsync(ExecutionMessage message, CancellationToken cancellationToken) =>
        await _writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);

    public async Task<ExecutionMessage?> ReceiveAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }
}

internal sealed class EchoInvoker : IScriptInvoker
{
    public Task<int> InvokeAsync(string script, IExecutionOutput output, CancellationToken cancellationToken)
    {
        output.WriteStdout(script);
        output.WriteStderr("err");
        return Task.FromResult(7);
    }
}

internal sealed class ThrowingInvoker : IScriptInvoker
{
    public Task<int> InvokeAsync(string script, IExecutionOutput output, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("boom");
}

internal sealed class BlockingInvoker : IScriptInvoker
{
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started => _started.Task;

    public Task<int> InvokeAsync(string script, IExecutionOutput output, CancellationToken cancellationToken)
    {
        _started.TrySetResult();
        var done = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken.Register(() => done.TrySetCanceled(cancellationToken));
        return done.Task;
    }
}

internal sealed class OneByteStream : Stream
{
    private readonly byte[] _data;
    private int _position;

    public OneByteStream(byte[] data) => _data = data;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _data.Length;
    public override long Position { get => _position; set => throw new NotSupportedException(); }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= _data.Length)
            return 0;

        buffer[offset] = _data[_position++];
        return 1;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_position >= _data.Length)
            return ValueTask.FromResult(0);

        buffer.Span[0] = _data[_position++];
        return ValueTask.FromResult(1);
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
