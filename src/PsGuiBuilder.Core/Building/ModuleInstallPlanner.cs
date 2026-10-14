// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text.RegularExpressions;
using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Core.Building;

public enum ModuleInstallKind
{
    PsGallery,
    WindowsCapability,
    WindowsOptionalFeature
}

public sealed class ModuleInstallPlan
{
    public ModuleInstallPlan(
        string moduleName,
        ModuleInstallKind kind,
        string target,
        bool requiresAdmin,
        PowerShellEdition edition,
        string summary,
        string script,
        string presenceScript)
    {
        ModuleName = moduleName;
        Kind = kind;
        Target = target;
        RequiresAdmin = requiresAdmin;
        Edition = edition;
        Summary = summary;
        Script = script;
        PresenceScript = presenceScript;
    }

    public string ModuleName { get; }

    public ModuleInstallKind Kind { get; }

    public string Target { get; }

    public bool RequiresAdmin { get; }

    public PowerShellEdition Edition { get; }

    public string Summary { get; }

    public string Script { get; }

    public string PresenceScript { get; }
}

public static partial class ModuleInstallPlanner
{
    private static readonly HashSet<string> InboxModules = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.PowerShell.Core",
        "Microsoft.PowerShell.Management",
        "Microsoft.PowerShell.Utility",
        "Microsoft.PowerShell.Security",
        "Microsoft.PowerShell.Diagnostics",
        "Microsoft.PowerShell.Host",
        "Microsoft.PowerShell.LocalAccounts",
        "Microsoft.PowerShell.Archive",
        "Microsoft.WSMan.Management",
        "CimCmdlets",
        "PSDiagnostics",
        "PSDesiredStateConfiguration",
        "NetSecurity",
        "NetTCPIP",
        "NetAdapter",
        "DnsClient",
        "SmbShare",
        "Storage",
        "ScheduledTasks",
        "PrintManagement",
        "Dism",
        "Appx",
        "PackageManagement",
        "PowerShellGet",
        "PKI",
        "Defender",
        "International",
        "ThreadJob",
        "PnpDevice",
        "SecureBoot",
        "BitLocker"
    };

    private static readonly Dictionary<string, string> RsatCapabilities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ActiveDirectory"] = "Rsat.ActiveDirectory.DS-LDS.Tools~~~~0.0.1.0",
        ["ADDSDeployment"] = "Rsat.ActiveDirectory.DS-LDS.Tools~~~~0.0.1.0",
        ["GroupPolicy"] = "Rsat.GroupPolicy.Management.Tools~~~~0.0.1.0",
        ["DnsServer"] = "Rsat.Dns.Tools~~~~0.0.1.0",
        ["DhcpServer"] = "Rsat.DHCP.Tools~~~~0.0.1.0",
        ["ServerManager"] = "Rsat.ServerManager.Tools~~~~0.0.1.0",
        ["IPAM"] = "Rsat.IPAM.Client.Tools~~~~0.0.1.0",
        ["NetworkController"] = "Rsat.NetworkController.Tools~~~~0.0.1.0",
        ["NetworkLoadBalancingClusters"] = "Rsat.NLB.Tools~~~~0.0.1.0",
        ["RemoteAccess"] = "Rsat.RemoteAccess.Management.Tools~~~~0.0.1.0",
        ["RemoteDesktop"] = "Rsat.RemoteDesktop.Services.Tools~~~~0.0.1.0",
        ["FailoverClusters"] = "Rsat.FailoverCluster.Management.Tools~~~~0.0.1.0"
    };

    private static readonly Dictionary<string, string> OptionalFeatures = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Hyper-V"] = "Microsoft-Hyper-V-Management-PowerShell"
    };

    public static bool TryCreate(string? moduleName, PowerShellEdition edition, out ModuleInstallPlan? plan)
    {
        plan = null;
        var name = moduleName?.Trim() ?? "";
        if (!ModuleNamePattern().IsMatch(name) || InboxModules.Contains(name))
            return false;

        var presence = PresenceScript(name);
        if (RsatCapabilities.TryGetValue(name, out var capability))
        {
            plan = new ModuleInstallPlan(
                name,
                ModuleInstallKind.WindowsCapability,
                capability,
                requiresAdmin: true,
                edition,
                name + " is not installed."
                    + Environment.NewLine + Environment.NewLine
                    + "Shellmason can add the Windows capability " + capability + " with Add-WindowsCapability."
                    + Environment.NewLine
                    + "This changes the machine and needs administrator approval."
                    + Environment.NewLine + Environment.NewLine
                    + "Nothing is installed until you choose Yes.",
                CapabilityScript(capability),
                presence);
            return true;
        }

        if (OptionalFeatures.TryGetValue(name, out var feature))
        {
            plan = new ModuleInstallPlan(
                name,
                ModuleInstallKind.WindowsOptionalFeature,
                feature,
                requiresAdmin: true,
                edition,
                name + " is not installed."
                    + Environment.NewLine + Environment.NewLine
                    + "Shellmason can enable the Windows optional feature " + feature + "."
                    + Environment.NewLine
                    + "This changes the machine and needs administrator approval."
                    + Environment.NewLine + Environment.NewLine
                    + "Nothing is installed until you choose Yes.",
                FeatureScript(feature),
                presence);
            return true;
        }

        plan = new ModuleInstallPlan(
            name,
            ModuleInstallKind.PsGallery,
            name,
            requiresAdmin: false,
            edition,
            name + " is not installed."
                + Environment.NewLine + Environment.NewLine
                + "Shellmason can install it for the current user from PSGallery with Install-Module -Scope CurrentUser."
                + Environment.NewLine
                + "If the NuGet package provider is missing, it is installed for the current user."
                + Environment.NewLine
                + "If PSGallery is untrusted, it is trusted for this install and then set back."
                + Environment.NewLine + Environment.NewLine
                + "Nothing is installed until you choose Yes.",
            GalleryScript(name),
            presence);
        return true;
    }

    public static string PresenceScript(string moduleName)
    {
        var quoted = Quote(moduleName);
        return "$ErrorActionPreference = 'Stop'\r\n"
            + "$ProgressPreference = 'SilentlyContinue'\r\n"
            + "$found = @(Get-Module -ListAvailable -Name " + quoted + ")\r\n"
            + "if ($found.Count -gt 0) { 'installed' } else { 'missing' }\r\n";
    }

    private static string GalleryScript(string moduleName)
    {
        var quoted = Quote(moduleName);
        return "$ErrorActionPreference = 'Stop'\r\n"
            + "$ProgressPreference = 'SilentlyContinue'\r\n"
            + "$ConfirmPreference = 'None'\r\n"
            + "$provider = Get-PackageProvider -Name NuGet -ErrorAction SilentlyContinue | Where-Object { $_.Version -ge [version]'2.8.5.201' } | Select-Object -First 1\r\n"
            + "if ($null -eq $provider) {\r\n"
            + "  Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -Scope CurrentUser | Out-Null\r\n"
            + "}\r\n"
            + "$repo = Get-PSRepository -Name PSGallery\r\n"
            + "$previous = [string]$repo.InstallationPolicy\r\n"
            + "if ($previous -ne 'Trusted') {\r\n"
            + "  Set-PSRepository -Name PSGallery -InstallationPolicy Trusted\r\n"
            + "}\r\n"
            + "try {\r\n"
            + "  Install-Module -Name " + quoted + " -Scope CurrentUser -Repository PSGallery -Force -AllowClobber -Confirm:$false\r\n"
            + "} finally {\r\n"
            + "  if ($previous -ne 'Trusted') {\r\n"
            + "    Set-PSRepository -Name PSGallery -InstallationPolicy $previous\r\n"
            + "  }\r\n"
            + "}\r\n";
    }

    private static string CapabilityScript(string capability) =>
        "$ErrorActionPreference = 'Stop'\r\n"
        + "$ProgressPreference = 'SilentlyContinue'\r\n"
        + "$ConfirmPreference = 'None'\r\n"
        + "Add-WindowsCapability -Online -Name " + Quote(capability) + "\r\n";

    private static string FeatureScript(string feature) =>
        "$ErrorActionPreference = 'Stop'\r\n"
        + "$ProgressPreference = 'SilentlyContinue'\r\n"
        + "$ConfirmPreference = 'None'\r\n"
        + "Enable-WindowsOptionalFeature -Online -FeatureName " + Quote(feature) + " -All -NoRestart\r\n";

    private static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9._-]{0,127}$")]
    private static partial Regex ModuleNamePattern();
}
