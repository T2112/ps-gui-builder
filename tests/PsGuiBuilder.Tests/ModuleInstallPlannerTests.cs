// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.Building;
using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Tests;

public class ModuleInstallPlannerTests
{
    [Fact]
    public void Gallery_install_is_current_user_and_waits_for_confirmation()
    {
        Assert.True(ModuleInstallPlanner.TryCreate("ExchangeOnlineManagement", PowerShellEdition.PowerShell7, out var plan));
        Assert.NotNull(plan);
        Assert.Equal(ModuleInstallKind.PsGallery, plan!.Kind);
        Assert.False(plan.RequiresAdmin);
        Assert.Contains("Install-Module -Scope CurrentUser", plan.Summary, StringComparison.Ordinal);
        Assert.Contains("Nothing is installed until you choose Yes.", plan.Summary, StringComparison.Ordinal);
        Assert.Contains("Install-PackageProvider -Name NuGet", plan.Script, StringComparison.Ordinal);
        Assert.Contains("-Scope CurrentUser", plan.Script, StringComparison.Ordinal);
        Assert.Contains("Set-PSRepository -Name PSGallery -InstallationPolicy Trusted", plan.Script, StringComparison.Ordinal);
        Assert.Contains("Set-PSRepository -Name PSGallery -InstallationPolicy $previous", plan.Script, StringComparison.Ordinal);
        Assert.DoesNotContain("-Scope AllUsers", plan.Script, StringComparison.Ordinal);
        Assert.Contains("'ExchangeOnlineManagement'", plan.Script, StringComparison.Ordinal);
    }

    [Fact]
    public void Active_directory_uses_a_windows_capability_and_hyper_v_uses_an_optional_feature()
    {
        Assert.True(ModuleInstallPlanner.TryCreate("ActiveDirectory", PowerShellEdition.WindowsPowerShell5, out var ad));
        Assert.Equal(ModuleInstallKind.WindowsCapability, ad!.Kind);
        Assert.True(ad.RequiresAdmin);
        Assert.Contains("Rsat.ActiveDirectory.DS-LDS.Tools~~~~0.0.1.0", ad.Summary, StringComparison.Ordinal);
        Assert.Contains("Add-WindowsCapability -Online -Name 'Rsat.ActiveDirectory.DS-LDS.Tools~~~~0.0.1.0'", ad.Script, StringComparison.Ordinal);
        Assert.Contains("Nothing is installed until you choose Yes.", ad.Summary, StringComparison.Ordinal);

        Assert.True(ModuleInstallPlanner.TryCreate("Hyper-V", PowerShellEdition.PowerShell7, out var hyperv));
        Assert.Equal(ModuleInstallKind.WindowsOptionalFeature, hyperv!.Kind);
        Assert.Contains("Enable-WindowsOptionalFeature -Online -FeatureName 'Microsoft-Hyper-V-Management-PowerShell'", hyperv.Script, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_unsafe_names_and_does_not_plan_an_inbox_module()
    {
        Assert.False(ModuleInstallPlanner.TryCreate("NetSecurity", PowerShellEdition.PowerShell7, out _));
        Assert.False(ModuleInstallPlanner.TryCreate("foo; Remove-Item", PowerShellEdition.PowerShell7, out _));
        Assert.False(ModuleInstallPlanner.TryCreate("", PowerShellEdition.PowerShell7, out _));
        Assert.False(ModuleInstallPlanner.TryCreate(null, PowerShellEdition.PowerShell7, out _));
    }
}
