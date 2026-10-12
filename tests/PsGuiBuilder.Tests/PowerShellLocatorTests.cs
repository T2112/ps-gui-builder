// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.CommandTree;
using PsGuiBuilder.Core.Execution;

namespace PsGuiBuilder.Tests;

public class PowerShellLocatorTests
{
    [Fact]
    public void Prefers_pwsh_and_falls_back_to_windows_powershell_with_a_message()
    {
        var fallback = PowerShellLocator.Resolve(
            PowerShellEdition.PowerShell7,
            _ => null,
            path => path.EndsWith("powershell.exe", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(PowerShellLocator.PwshFallbackMessage, fallback.Notice);
        Assert.NotNull(fallback.Shell);
        Assert.EndsWith("powershell.exe", fallback.Shell!.FileName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_command_can_force_windows_powershell_even_when_pwsh_is_configured()
    {
        var forced = PowerShellLocator.Resolve(
            PowerShellEdition.WindowsPowerShell5,
            _ => "pwsh",
            path => path.EndsWith("powershell.exe", StringComparison.OrdinalIgnoreCase));

        Assert.Null(forced.Notice);
        Assert.EndsWith("powershell.exe", forced.Shell!.FileName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Environment_override_selects_the_pwsh_edition()
    {
        var chosen = PowerShellLocator.Resolve(
            PowerShellEdition.PowerShell7,
            name => name == PowerShellShell.ShellEnvironmentVariable ? "pwsh" : null,
            _ => false);

        Assert.Equal("pwsh", chosen.Shell!.FileName);
        Assert.Null(chosen.Notice);
        Assert.Null(chosen.Error);
    }
}
