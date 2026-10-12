// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Net;
using System.Net.Sockets;
using System.Text;
using PsGuiBuilder.Core.Execution;
using PsGuiBuilder.Tests.Support;

namespace PsGuiBuilder.Tests;

public class ExecutionProtocolTests
{
    [Fact]
    public async Task Frames_roundtrip_one_byte_at_a_time()
    {
        var first = MessageFramer.Frame(Encoding.UTF8.GetBytes("{\"kind\":\"stdout\",\"text\":\"hi\"}"));
        var second = MessageFramer.Frame(ExecutionMessageCodec.ToUtf8(ExecutionMessage.ExitMessage(4)));
        var stream = new OneByteStream(first.Concat(second).ToArray());

        var stdout = ExecutionMessageCodec.FromUtf8((await MessageFramer.ReadPayloadAsync(stream, CancellationToken.None))!);
        var exit = ExecutionMessageCodec.FromUtf8((await MessageFramer.ReadPayloadAsync(stream, CancellationToken.None))!);

        Assert.Equal(ExecutionKinds.Stdout, stdout.Kind);
        Assert.Equal("hi", stdout.Text);
        Assert.Equal(4, exit.Code);
        Assert.Null(await MessageFramer.ReadPayloadAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_a_truncated_frame()
    {
        var stream = new MemoryStream(new byte[] { 10, 0, 0, 0, 1, 2, 3 });
        await Assert.ThrowsAsync<EndOfStreamException>(() => MessageFramer.ReadPayloadAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_an_oversized_frame_without_allocating_it()
    {
        var stream = new MemoryStream(new byte[] { 0xff, 0xff, 0xff, 0x7f });
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => MessageFramer.ReadPayloadAsync(stream, CancellationToken.None));
        Assert.Contains("length", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stream_transport_roundtrips_on_a_memory_stream()
    {
        var stream = new MemoryStream();
        var transport = new StreamFrameTransport(stream);
        await transport.SendAsync(ExecutionMessage.StdoutMessage("hi"), CancellationToken.None);
        await transport.SendAsync(ExecutionMessage.ExitMessage(2), CancellationToken.None);
        stream.Position = 0;

        var first = await transport.ReceiveAsync(CancellationToken.None);
        var second = await transport.ReceiveAsync(CancellationToken.None);

        Assert.Equal("hi", first!.Text);
        Assert.Equal(2, second!.Code);
    }

    [Fact]
    public async Task Host_streams_output_and_returns_the_exit_code()
    {
        var (parent, child) = ChannelFrameTransport.CreatePair();
        var output = new CollectingOutput();
        var host = new ElevatedHostWorker().RunAsync(child, new EchoInvoker(), CancellationToken.None);
        var code = await new ExecutionClient().RunAsync(parent, "Get-Date", output, CancellationToken.None);
        await host.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(7, code);
        Assert.Equal("Get-Date", output.Stdout);
        Assert.Equal("err", output.Stderr);
    }

    [Fact]
    public async Task Host_cancel_stops_the_script()
    {
        var (parent, child) = ChannelFrameTransport.CreatePair();
        var invoker = new BlockingInvoker();
        var host = new ElevatedHostWorker().RunAsync(child, invoker, CancellationToken.None);
        using var cts = new CancellationTokenSource();
        var client = new ExecutionClient().RunAsync(parent, "Start-Sleep", new CollectingOutput(), cts.Token);

        await invoker.Started.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client);
        await host.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Host_errors_surface_to_the_client()
    {
        var (parent, child) = ChannelFrameTransport.CreatePair();
        var host = new ElevatedHostWorker().RunAsync(child, new ThrowingInvoker(), CancellationToken.None);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ExecutionClient().RunAsync(parent, "Get-Item", new CollectingOutput(), CancellationToken.None));
        await host.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("boom", error.Message);
    }

    [Fact]
    public async Task Socket_pair_carries_a_script_to_completion()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        using var server = await listener.AcceptTcpClientAsync();
        listener.Stop();
        await using var clientStream = client.GetStream();
        await using var serverStream = server.GetStream();

        var host = new ElevatedHostWorker().RunAsync(new StreamFrameTransport(serverStream), new EchoInvoker(), CancellationToken.None);
        var output = new CollectingOutput();
        var code = await new ExecutionClient().RunAsync(new StreamFrameTransport(clientStream), "ping", output, CancellationToken.None);
        await host.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(7, code);
        Assert.Equal("ping", output.Stdout);
    }
}
