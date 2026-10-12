// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text;
using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Core.Building;

public static class ScriptComposer
{
    public static string ComposePreview(CommandDefinition command, string renderedCommand) =>
        Compose(command, renderedCommand, signInUsername: null, includeCredentialSetup: false);

    public static string ComposeExecution(CommandDefinition command, string renderedCommand, string? signInUsername = null) =>
        Compose(command, renderedCommand, signInUsername, includeCredentialSetup: true);

    private static string Compose(
        CommandDefinition command,
        string renderedCommand,
        string? signInUsername,
        bool includeCredentialSetup)
    {
        var builder = new StringBuilder();
        if (includeCredentialSetup)
        {
            builder.AppendLine("$ErrorActionPreference = 'Stop'");
            builder.AppendLine("$ProgressPreference = 'SilentlyContinue'");
        }

        if (!string.IsNullOrEmpty(command.Module) && !IsCoreModule(command.Module))
            builder.Append("Import-Module ").AppendLine(command.Module);

        if (command.SignIn?.Mode == SignInMode.Credential && includeCredentialSetup && signInUsername is not null)
        {
            builder.Append("$__psgui_username = ").AppendLine(ParameterSyntax.QuoteSingle(signInUsername));
            builder.Append("$__psgui_credential = New-Object System.Management.Automation.PSCredential ($__psgui_username, ");
            builder.Append(ParameterSyntax.SecretVariable(SignInRequirement.SecretKey));
            builder.AppendLine(")");
        }

        if (command.SignIn is not null)
            builder.AppendLine(command.SignIn.Command);

        builder.AppendLine(renderedCommand);
        return builder.ToString().TrimEnd() + (includeCredentialSetup ? Environment.NewLine : "");
    }

    private static bool IsCoreModule(string module) =>
        module.Equals("Microsoft.PowerShell.Core", StringComparison.OrdinalIgnoreCase);
}
