// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text;

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

public static class PowerShellLocator
{
    public static PowerShellShell Resolve()
    {
        var overridePath = Environment.GetEnvironmentVariable(PowerShellShell.ShellEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
            return PowerShellShell.ForExecutable(overridePath.Trim());

        if (OperatingSystem.IsWindows())
            return PowerShellShell.WindowsPowerShell();

        return PowerShellShell.ForExecutable("pwsh");
    }
}

public static class PowerShellCommandEncoding
{
    public static string ToEncodedCommand(string script) =>
        Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

    public static string FromEncodedCommand(string encoded) =>
        Encoding.Unicode.GetString(Convert.FromBase64String(encoded));
}
