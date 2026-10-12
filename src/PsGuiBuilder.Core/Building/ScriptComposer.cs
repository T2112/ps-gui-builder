// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text;
using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Core.Building;

public static class ScriptComposer
{
    public static string ComposePreview(CommandDefinition command, string renderedCommand)
    {
        if (string.IsNullOrEmpty(command.Module))
            return renderedCommand;

        return "Import-Module " + command.Module + Environment.NewLine + renderedCommand;
    }

    public static string ComposeExecution(CommandDefinition command, string renderedCommand)
    {
        var builder = new StringBuilder();
        builder.AppendLine("$ErrorActionPreference = 'Stop'");
        builder.AppendLine("$ProgressPreference = 'SilentlyContinue'");
        if (!string.IsNullOrEmpty(command.Module))
            builder.Append("Import-Module ").AppendLine(command.Module);
        builder.AppendLine(renderedCommand);
        return builder.ToString();
    }
}
