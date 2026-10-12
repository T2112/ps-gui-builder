// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Diagnostics;
using System.Text;

namespace PsGuiBuilder.Core.Execution;

public sealed class ProcessScriptInvoker : IScriptInvoker
{
    private const int MaxEncodedCommandLength = 30_000;
    private readonly PowerShellShell _shell;

    public ProcessScriptInvoker(PowerShellShell shell)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
    }

    public async Task<int> InvokeAsync(string script, IExecutionOutput output, CancellationToken cancellationToken)
    {
        if (output is null)
            throw new ArgumentNullException(nameof(output));

        var startInfo = new ProcessStartInfo
        {
            FileName = _shell.FileName,
            Arguments = _shell.Arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = _shell.Encoding,
            StandardErrorEncoding = _shell.Encoding
        };

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(profile) && Directory.Exists(profile))
            startInfo.WorkingDirectory = profile;

        string? tempFile = null;
        if (_shell.Delivery == ScriptDelivery.StandardInput)
        {
            startInfo.RedirectStandardInput = true;
            startInfo.StandardInputEncoding = _shell.Encoding;
        }
        else
        {
            var encoded = PowerShellCommandEncoding.ToEncodedCommand(script);
            if (encoded.Length <= MaxEncodedCommandLength)
            {
                startInfo.Arguments = _shell.Arguments + " -EncodedCommand " + encoded;
            }
            else
            {
                tempFile = Path.Combine(Path.GetTempPath(), "PsGuiBuilder-" + Guid.NewGuid().ToString("N") + ".ps1");
                await File.WriteAllTextAsync(tempFile, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken)
                    .ConfigureAwait(false);
                startInfo.Arguments = _shell.Arguments + " -File " + QuoteArgument(tempFile);
            }
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        Task? stdout = null;
        Task? stderr = null;
        try
        {
            if (!process.Start())
                throw new InvalidOperationException($"Could not start '{_shell.FileName}'.");

            using var killOnCancel = cancellationToken.Register(() => TryKill(process));
            stdout = PumpAsync(process.StandardOutput, output.WriteStdout, cancellationToken);
            stderr = PumpAsync(process.StandardError, output.WriteStderr, cancellationToken);

            if (_shell.Delivery == ScriptDelivery.StandardInput)
            {
                await process.StandardInput.WriteAsync(script.AsMemory(), cancellationToken).ConfigureAwait(false);
                process.StandardInput.Close();
            }

            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new OperationCanceledException("The script was cancelled.", ex, cancellationToken);
        }
        finally
        {
            TryKill(process);
            if (stdout is not null && stderr is not null)
            {
                try
                {
                    await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            }

            if (tempFile is not null)
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }

    private static async Task PumpAsync(StreamReader reader, Action<string> write, CancellationToken cancellationToken)
    {
        var buffer = new char[512];
        var first = true;
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (count == 0)
                return;

            var text = new string(buffer, 0, count);
            if (first)
            {
                text = text.TrimStart('\uFEFF');
                first = false;
            }

            if (text.Length > 0)
                write(text);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    private static string QuoteArgument(string value) =>
        "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
