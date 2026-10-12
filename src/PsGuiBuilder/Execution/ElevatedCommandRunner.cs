// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using PsGuiBuilder.Core.Execution;

namespace PsGuiBuilder.Execution;

public sealed class ElevatedConnection : IAsyncDisposable
{
    private readonly NamedPipeServerStream _server;
    private readonly CancellationTokenSource _connectCts;
    private readonly Task _connectTask;
    private readonly StreamFrameTransport _transport;
    private int _disposed;

    internal ElevatedConnection(NamedPipeServerStream server, CancellationTokenSource connectCts, Task connectTask)
    {
        _server = server;
        _connectCts = connectCts;
        _connectTask = connectTask;
        _transport = new StreamFrameTransport(server);
    }

    public Task<int> InvokeAsync(ScriptRequest request, IExecutionOutput output, CancellationToken cancellationToken) =>
        new ExecutionClient().RunAsync(_transport, request, output, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _connectCts.Cancel();
        try
        {
            await _connectTask.ConfigureAwait(false);
        }
        catch (Exception)
        {
        }

        _connectCts.Dispose();
        await _server.DisposeAsync().ConfigureAwait(false);
    }
}

public sealed class ElevatedCommandRunner
{
    public async Task<int> RunAsync(ScriptRequest request, IExecutionOutput output, CancellationToken cancellationToken)
    {
        await using var connection = await ConnectAsync(cancellationToken).ConfigureAwait(false);
        return await connection.InvokeAsync(request, output, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ElevatedConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        var pipeName = "PsGuiBuilder." + Guid.NewGuid().ToString("N");
        var server = CreateServer(pipeName);
        var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var connectTask = server.WaitForConnectionAsync(connectCts.Token);
        try
        {
            try
            {
                await Task.Run(() => StartElevated(pipeName)).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                throw new OperationCanceledException("Administrator elevation was declined.", ex, cancellationToken);
            }

            try
            {
                await connectTask.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException("The elevated helper did not connect after administrator approval.");
            }

            return new ElevatedConnection(server, connectCts, connectTask);
        }
        catch
        {
            connectCts.Cancel();
            try
            {
                await connectTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            connectCts.Dispose();
            await server.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static void StartElevated(string pipeName)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = FindHostExecutable(),
            Arguments = "--pipe " + pipeName,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows did not start the elevated helper.");
    }

    public static string FindHostExecutable()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "elevated-host", "PsGuiBuilder.ElevatedHost.exe");
        if (!File.Exists(path))
            throw new FileNotFoundException("The elevated helper is missing. Rebuild the application.", path);

        return path;
    }

    private static NamedPipeServerStream CreateServer(string pipeName)
    {
        var user = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The current Windows user could not be identified.");
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 65536,
            outBufferSize: 65536,
            pipeSecurity: security);
    }
}
