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
    public void Parameter_set_selects_members_and_validate_set_becomes_choices()
    {
        const string json = """
        {
          "name": "Get-ChildItem",
          "module": "Microsoft.PowerShell.Management",
          "synopsis": "Gets the items in a location.",
          "parameterSets": [
            { "name": "Items", "isDefault": true },
            { "name": "LiteralItems", "isDefault": false }
          ],
          "parameters": [
            { "name": "Path", "kind": "string", "mandatory": false, "sets": ["Items"], "mandatorySets": ["Items"] },
            { "name": "LiteralPath", "kind": "string", "mandatory": false, "sets": ["LiteralItems"], "mandatorySets": ["LiteralItems"] },
            { "name": "Attributes", "kind": "string", "mandatory": false, "sets": ["Items", "LiteralItems"], "mandatorySets": [], "choices": ["Directory", "Hidden"] }
          ]
        }
        """;

        var view = CommandMetadataMapper.Read(json);
        Assert.Equal(new[] { "Items", "LiteralItems" }, view.ParameterSets);
        Assert.Equal("Items", view.ParameterSet);
        Assert.Equal(new[] { "Path", "Attributes" }, view.Command.Parameters.Select(parameter => parameter.Name).ToArray());
        Assert.True(view.Command.Parameters[0].Required);
        Assert.Equal(ParameterType.Choice, view.Command.Parameters[1].Type);
        Assert.Equal(new[] { "Directory", "Hidden" }, view.Command.Parameters[1].Choices);
        Assert.Equal("Gets the items in a location.", view.Command.Description);

        var literal = CommandMetadataMapper.FromSnapshot(json, "LiteralItems", PowerShellEdition.WindowsPowerShell5);
        Assert.Equal(new[] { "LiteralPath", "Attributes" }, literal.Parameters.Select(parameter => parameter.Name).ToArray());
        Assert.True(literal.Parameters[0].Required);
        Assert.Equal(PowerShellEdition.WindowsPowerShell5, literal.Shell);
        Assert.Equal(new[] { "Get-ChildItem", "LiteralItems" }, literal.Path);
    }

    [Fact]
    public void Probe_script_asks_get_command_for_the_environment_name()
    {
        Assert.Contains("$env:PSGUIBUILDER_COMMAND", CommandMetadataProbe.ProbeScript, StringComparison.Ordinal);
        Assert.Contains("Get-Command -Name $requested", CommandMetadataProbe.ProbeScript, StringComparison.Ordinal);
        Assert.Contains("Get-Help -Name $command.Name", CommandMetadataProbe.ProbeScript, StringComparison.Ordinal);
        Assert.Contains("ValidateSetAttribute", CommandMetadataProbe.ProbeScript, StringComparison.Ordinal);
        Assert.Contains("parameterSets", CommandMetadataProbe.ProbeScript, StringComparison.Ordinal);
        Assert.DoesNotContain("Update-Help", CommandMetadataProbe.ProbeScript, StringComparison.Ordinal);
    }
}
