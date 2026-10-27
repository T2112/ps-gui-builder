// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Tests.Support;

namespace PsGuiBuilder.Tests;

public class RepositoryPresentationTests
{
    private static string Root => Path.GetDirectoryName(Repo.CommandTreePath())!;

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)));

    [Fact]
    public void Readme_leads_with_the_product_and_keeps_the_ownership_statement()
    {
        var readme = Read("README.md");
        const string ownership =
            "This project is a personal side project created by Thomas Schueler (T2112 / thomas@schuelertech.com) entirely on personal time using personal equipment and resources. It is not related to any employment, has no connection to any employer, and no employer resources, time, code, or intellectual property were used in its creation or development.";

        var value = readme.IndexOf("Shellmason builds a PowerShell command from cascading menus", StringComparison.Ordinal);
        var what = readme.IndexOf("## What it is", StringComparison.Ordinal);
        var shots = readme.IndexOf("## Screenshots", StringComparison.Ordinal);
        var install = readme.IndexOf("## Install", StringComparison.Ordinal);
        var usage = readme.IndexOf("## Usage", StringComparison.Ordinal);
        var stack = readme.IndexOf("## Tech stack", StringComparison.Ordinal);
        var architecture = readme.IndexOf("## Architecture", StringComparison.Ordinal);
        var links = readme.IndexOf("## Changelog and issues", StringComparison.Ordinal);
        var owned = readme.IndexOf(ownership, StringComparison.Ordinal);

        Assert.True(value >= 0 && value < what);
        Assert.True(what < shots && shots < install && install < usage);
        Assert.True(usage < stack && stack < architecture && architecture < links && links < owned);
        Assert.Contains("Personal project by Thomas Schueler, built on personal time and equipment. See [OWNERSHIP.md](OWNERSHIP.md).", readme, StringComparison.Ordinal);
        Assert.Contains("**Personal Project — Developed on Personal Time and Equipment**", readme, StringComparison.Ordinal);
        Assert.Contains("All rights reserved to the author unless otherwise licensed.", readme, StringComparison.Ordinal);
        Assert.Contains("Shellmason (repository: ps-gui-builder, executable: PsGuiBuilder.exe).", readme, StringComparison.Ordinal);
        Assert.Equal(1, Count(readme, "repository: ps-gui-builder, executable: PsGuiBuilder.exe"));
        Assert.Contains("docs/screenshots/README.md", readme, StringComparison.Ordinal);
        Assert.Contains("https://github.com/T2112/ps-gui-builder/releases/latest", readme, StringComparison.Ordinal);
        Assert.Contains("SmartScreen", readme, StringComparison.Ordinal);
        Assert.Contains("scripts/New-ReleaseNotes.ps1", readme, StringComparison.Ordinal);
        Assert.Contains("[CHANGELOG.md](CHANGELOG.md)", readme, StringComparison.Ordinal);
        Assert.Contains("https://github.com/T2112/ps-gui-builder/issues", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void License_is_standard_mit_and_the_addendum_stays_in_ownership()
    {
        var license = Read("LICENSE").Replace("\r\n", "\n", StringComparison.Ordinal);
        var ownership = Read("OWNERSHIP.md");

        Assert.StartsWith("MIT License\n\nCopyright (c) 2026 Thomas Schueler\n", license, StringComparison.Ordinal);
        Assert.Contains("THE SOFTWARE IS PROVIDED \"AS IS\"", license, StringComparison.Ordinal);
        Assert.EndsWith("SOFTWARE.\n", license, StringComparison.Ordinal);
        Assert.DoesNotContain("No employer resources", license, StringComparison.Ordinal);
        Assert.DoesNotContain("---", license, StringComparison.Ordinal);
        Assert.Contains("Personal project developed by Thomas Schueler on personal time using personal equipment.", ownership, StringComparison.Ordinal);
        Assert.Contains("Not related to employment. No employer resources were used. This is a personal side project.", ownership, StringComparison.Ordinal);
        Assert.Contains("Version 0.2.0 was built the same way: personal time, personal equipment, no employer resources.", ownership, StringComparison.Ordinal);
        Assert.Contains("Version 0.3.0 was built the same way: personal time, personal equipment, no employer resources.", ownership, StringComparison.Ordinal);
        Assert.Contains("No employer resources were used.", Read("ARCHITECTURE.md"), StringComparison.Ordinal);
    }

    [Fact]
    public void Issue_templates_security_note_and_screenshot_list_exist()
    {
        var shots = Read("docs/screenshots/README.md");
        Assert.Contains("cascading-menus.png", shots, StringComparison.Ordinal);
        Assert.Contains("sentence-form.png", shots, StringComparison.Ordinal);
        Assert.Contains("output-pane.png", shots, StringComparison.Ordinal);
        Assert.Contains("I Want To:", shots, StringComparison.Ordinal);
        Assert.Contains("not in the repository yet", shots, StringComparison.Ordinal);
        Assert.DoesNotContain(".png)", Read("README.md"), StringComparison.Ordinal);

        Assert.Contains("thomas@schuelertech.com", Read("SECURITY.md"), StringComparison.Ordinal);
        Assert.Contains("name: Bug report", Read(".github/ISSUE_TEMPLATE/bug_report.yml"), StringComparison.Ordinal);
        Assert.Contains("name: Feature request", Read(".github/ISSUE_TEMPLATE/feature_request.yml"), StringComparison.Ordinal);
        Assert.Contains("cascading menus and a fill-in sentence", Read("docs/showcase.md"), StringComparison.Ordinal);
        Assert.Contains("powershell", Read("docs/showcase.md"), StringComparison.Ordinal);
        Assert.Contains("command-builder", Read("docs/showcase.md"), StringComparison.Ordinal);
        Assert.Contains("InfoBeforeFile={#RepoRoot}\\OWNERSHIP.md", Read("installer/PsGuiBuilder.iss"), StringComparison.Ordinal);
    }

    private static int Count(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
