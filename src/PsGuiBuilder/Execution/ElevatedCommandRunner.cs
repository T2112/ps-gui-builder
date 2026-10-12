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

public sealed class ElevatedCommandRunner
{
    public async Task<int> RunAsync(ScriptRequest request, IExecutionOutput output, CancellationToken cancellationToken)
    {
        var pipeName = "PsGuiBuilder." + Guid.NewGuid().ToString("N");
        using var server = CreateServer(pipeName);
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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

            var transport = new StreamFrameTransport(server);
            return await new ExecutionClient().RunAsync(transport, request, output, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            connectCts.Cancel();
            try
            {
                await connectTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
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
