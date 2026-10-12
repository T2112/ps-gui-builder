// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text;
using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Core.Execution;

public enum ScriptDelivery
{
    StandardInput,
    EncodedCommand
}

public sealed class PowerShellShell
{
    public const string ShellEnvironmentVariable = "PSGUIBUILDER_SHELL";
    public const string LaunchArguments =
        "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -OutputFormat Text";

    public PowerShellShell(string fileName, string arguments, Encoding encoding, ScriptDelivery delivery)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("A shell executable is required.", nameof(fileName));

        FileName = fileName;
        Arguments = arguments;
        Encoding = encoding;
        Delivery = delivery;
    }

    public string FileName { get; }

    public string Arguments { get; }

    public Encoding Encoding { get; }

    public ScriptDelivery Delivery { get; }

    public static PowerShellShell ForExecutable(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var windowsPowerShell = name.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase);
        // Stdin and -EncodedCommand for Windows PowerShell stay UTF-16.
        // Captured stdout and stderr are decoded by ProcessOutputText, because
        // powershell.exe writes UTF-16 or UTF-8 depending on the code page.
        var encoding = windowsPowerShell
            ? Encoding.Unicode
            : new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        return new PowerShellShell(fileName, LaunchArguments, encoding, ScriptDelivery.EncodedCommand);
    }

    public static PowerShellShell WindowsPowerShell() =>
        ForExecutable(Path.Combine(
            Environment.SystemDirectory,
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe"));
}

public sealed class ShellResolution
{
    public ShellResolution(PowerShellShell? shell, string? notice, string? error)
    {
        Shell = shell;
        Notice = notice;
        Error = error;
    }

    public PowerShellShell? Shell { get; }

    public string? Notice { get; }

    public string? Error { get; }
}

public static class PowerShellLocator
{
    public const string PwshFallbackMessage =
        "PowerShell 7 (pwsh) was not found. Running with Windows PowerShell 5.1 instead.";

    public static ShellResolution Resolve(PowerShellEdition edition) =>
        Resolve(edition, static name => Environment.GetEnvironmentVariable(name), File.Exists);

    public static PowerShellShell Resolve() =>
        Resolve(PowerShellEdition.PowerShell7).Shell ?? PowerShellShell.ForExecutable("pwsh");

    public static ShellResolution Resolve(
        PowerShellEdition edition,
        Func<string, string?> environment,
        Func<string, bool> exists)
    {
        if (environment is null)
            throw new ArgumentNullException(nameof(environment));
        if (exists is null)
            throw new ArgumentNullException(nameof(exists));

        if (edition == PowerShellEdition.WindowsPowerShell5)
        {
            var windows = WindowsPowerShellPath();
            if (!exists(windows))
            {
                return new ShellResolution(
                    null,
                    null,
                    "Windows PowerShell 5.1 was not found at " + windows + ".");
            }

            return new ShellResolution(PowerShellShell.ForExecutable(windows), null, null);
        }

        var overridePath = environment(PowerShellShell.ShellEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            var chosen = overridePath.Trim();
            if (IsFilePath(chosen) && !exists(chosen))
            {
                return new ShellResolution(null, null, "PSGUIBUILDER_SHELL does not exist: " + chosen);
            }

            return new ShellResolution(PowerShellShell.ForExecutable(chosen), null, null);
        }

        var pwsh = FindPwsh(exists);
        if (pwsh is not null)
            return new ShellResolution(PowerShellShell.ForExecutable(pwsh), null, null);

        var fallback = WindowsPowerShellPath();
        if (exists(fallback))
            return new ShellResolution(PowerShellShell.ForExecutable(fallback), PwshFallbackMessage, null);

        return new ShellResolution(null, null, "PowerShell 7 (pwsh) was not found, and Windows PowerShell 5.1 is not available.");
    }

    private static string? FindPwsh(Func<string, bool> exists)
    {
        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(programFiles))
            {
                var installed = Path.Combine(programFiles, "PowerShell", "7", "pwsh.exe");
                if (exists(installed))
                    return installed;
            }
        }

        return FindOnPath(OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh", exists);
    }

    private static string? FindOnPath(string fileName, Func<string, bool> exists)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
            return null;

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, fileName);
            if (exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string WindowsPowerShellPath() =>
        Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

    private static bool IsFilePath(string value) =>
        Path.IsPathRooted(value) || value.Contains(Path.DirectorySeparatorChar) || value.Contains(Path.AltDirectorySeparatorChar);
}

public static class PowerShellCommandEncoding
{
    public static string ToEncodedCommand(string script) =>
        Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

    public static string FromEncodedCommand(string encoded) =>
        Encoding.Unicode.GetString(Convert.FromBase64String(encoded));
}
