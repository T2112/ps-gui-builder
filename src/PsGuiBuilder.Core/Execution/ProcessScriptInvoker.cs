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

    public async Task<int> InvokeAsync(ScriptRequest request, IExecutionOutput output, CancellationToken cancellationToken)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (output is null)
            throw new ArgumentNullException(nameof(output));

        var hasSecrets = request.Secrets.Count > 0;
        var arguments = request.Interactive
            ? _shell.Arguments.Replace(" -NonInteractive", "", StringComparison.Ordinal)
            : _shell.Arguments;
        var startInfo = new ProcessStartInfo
        {
            FileName = _shell.FileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = !request.Interactive,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(profile) && Directory.Exists(profile))
            startInfo.WorkingDirectory = profile;

        var script = hasSecrets ? SecretPrologue.Apply(request.Script, request.Secrets) : request.Script;
        string? tempFile = null;
        var standardInput = _shell.Delivery == ScriptDelivery.StandardInput || hasSecrets;
        if (standardInput)
        {
            startInfo.RedirectStandardInput = true;
            startInfo.StandardInputEncoding = _shell.Encoding;
            if (hasSecrets)
                startInfo.Arguments = arguments + " -Command -";
        }
        else
        {
            var encoded = PowerShellCommandEncoding.ToEncodedCommand(script);
            if (encoded.Length <= MaxEncodedCommandLength)
            {
                startInfo.Arguments = arguments + " -EncodedCommand " + encoded;
            }
            else
            {
                tempFile = Path.Combine(Path.GetTempPath(), "PsGuiBuilder-" + Guid.NewGuid().ToString("N") + ".ps1");
                await File.WriteAllTextAsync(tempFile, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken)
                    .ConfigureAwait(false);
                startInfo.Arguments = arguments + " -File " + QuoteArgument(tempFile);
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
            stdout = ProcessOutputText.CopyAsync(process.StandardOutput.BaseStream, output.WriteStdout, cancellationToken);
            stderr = ProcessOutputText.CopyAsync(process.StandardError.BaseStream, output.WriteStderr, cancellationToken);

            if (standardInput)
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
