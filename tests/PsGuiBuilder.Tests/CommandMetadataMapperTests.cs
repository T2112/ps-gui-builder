// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.Building;
using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Tests;

public class CommandMetadataMapperTests
{
    [Fact]
    public void Builds_a_sentence_from_get_command_metadata()
    {
        var command = CommandMetadataMapper.FromSnapshot("""
        {
          "name": "Get-ChildItem",
          "module": "Microsoft.PowerShell.Management",
          "parameters": [
            { "name": "Path", "kind": "array", "mandatory": false },
            { "name": "Recurse", "kind": "switch", "mandatory": false },
            { "name": "Depth", "kind": "int", "mandatory": false },
            { "name": "Verbose", "kind": "switch", "mandatory": false }
          ]
        }
        """);

        Assert.Equal("Get-ChildItem", command.Cmdlet);
        Assert.Equal(new[] { "Path", "Recurse", "Depth" }, command.Parameters.Select(parameter => parameter.Name).ToArray());
        Assert.Equal(ParameterType.Array, command.Parameters[0].Type);
        Assert.Equal(ParameterType.Switch, command.Parameters[1].Type);
        Assert.False(command.Parameters[2].Required);
        Assert.Contains("{Recurse}", command.Template, StringComparison.Ordinal);
        Assert.Contains("-Path {Path}", command.Template, StringComparison.Ordinal);
        Assert.Contains("Run Get-ChildItem with", command.Sentence, StringComparison.Ordinal);
        Assert.DoesNotContain("Verbose", command.Template, StringComparison.Ordinal);
    }

    [Fact]
    public void Probe_script_asks_get_command_for_the_environment_name()
    {
        Assert.Contains("$env:PSGUIBUILDER_COMMAND", CommandMetadataProbe.ProbeScript, StringComparison.Ordinal);
        Assert.Contains("Get-Command -Name $requested", CommandMetadataProbe.ProbeScript, StringComparison.Ordinal);
        Assert.DoesNotContain("Get-Help", CommandMetadataProbe.ProbeScript, StringComparison.Ordinal);
    }
}
