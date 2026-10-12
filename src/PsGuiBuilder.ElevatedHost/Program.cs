// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.IO.Pipes;
using System.Text.RegularExpressions;
using PsGuiBuilder.Core.Execution;

namespace PsGuiBuilder.ElevatedHost;

internal static partial class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 2
            || !string.Equals(args[0], "--pipe", StringComparison.Ordinal)
            || !PipeName().IsMatch(args[1]))
        {
            return 2;
        }

        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                args[1],
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await pipe.ConnectAsync(20_000).ConfigureAwait(false);
            var transport = new StreamFrameTransport(pipe);
            var invoker = new ProcessScriptInvoker(PowerShellLocator.Resolve());
            await new ElevatedHostWorker().RunAsync(transport, invoker, CancellationToken.None).ConfigureAwait(false);
            return 0;
        }
        catch (TimeoutException)
        {
            return 3;
        }
        catch (IOException)
        {
            return 3;
        }
        catch (Exception)
        {
            return 1;
        }
    }

    [GeneratedRegex("^PsGuiBuilder\\.[0-9a-fA-F]{32}$")]
    private static partial Regex PipeName();
}
