// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Diagnostics;
using System.Text;
using PsGuiBuilder.Core.Execution;
using PsGuiBuilder.Tests.Support;

namespace PsGuiBuilder.Tests;

public class ProcessScriptInvokerTests
{
    [Fact]
    public async Task Captures_stdout_stderr_and_exit_code()
    {
        var (invoker, script) = CreateShell(
            unix: "printf 'hello\\n'; printf 'bad\\n' >&2; exit 3\n",
            windows: "[Console]::Out.WriteLine('hello'); [Console]::Error.WriteLine('bad'); exit 3");

        var output = new CollectingOutput();
        var code = await invoker.InvokeAsync(script, output, CancellationToken.None);

        Assert.Equal(3, code);
        Assert.Contains("hello", output.Stdout, StringComparison.Ordinal);
        Assert.Contains("bad", output.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancel_stops_a_long_running_script()
    {
        var (invoker, script) = CreateShell(
            unix: "sleep 30\n",
            windows: "Start-Sleep -Seconds 30");

        using var cts = new CancellationTokenSource();
        var started = Stopwatch.StartNew();
        var running = invoker.InvokeAsync(script, new CollectingOutput(), cts.Token);
        await Task.Delay(400);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(15), $"Cancellation took {started.Elapsed}.");
    }

    private static (ProcessScriptInvoker Invoker, string Script) CreateShell(string unix, string windows)
    {
        if (File.Exists("/bin/sh"))
        {
            var shell = new PowerShellShell("/bin/sh", "-s", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ScriptDelivery.StandardInput);
            return (new ProcessScriptInvoker(shell), unix);
        }

        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        if (File.Exists(powershell))
            return (new ProcessScriptInvoker(PowerShellShell.ForExecutable(powershell)), windows);

        Assert.Fail("No shell was available to exercise process execution.");
        throw new InvalidOperationException();
    }
}
