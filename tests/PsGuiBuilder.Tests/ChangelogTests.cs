// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.Updates;
using PsGuiBuilder.Tests.Support;

namespace PsGuiBuilder.Tests;

public class ChangelogTests
{
    private static string ChangelogText() =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(Repo.CommandTreePath())!, "CHANGELOG.md"));

    [Fact]
    public void History_sections_match_the_shipped_versions()
    {
        var changelog = ChangelogText();
        var current = Changelog.Section(changelog, "0.3.0");
        var icon = Changelog.Section(changelog, "v0.2.0");
        var first = Changelog.Section(changelog, "0.1.0");

        Assert.Contains("Shellmason", current, StringComparison.Ordinal);
        Assert.Contains("Get-Module -ListAvailable", current, StringComparison.Ordinal);
        Assert.Contains("no token", current, StringComparison.Ordinal);
        Assert.Contains("115 commands", current, StringComparison.Ordinal);
        Assert.DoesNotContain("86 commands", current, StringComparison.Ordinal);

        Assert.Contains("app icon", icon, StringComparison.Ordinal);
        Assert.Contains("15 categories and 86 commands", icon, StringComparison.Ordinal);
        Assert.Contains("Load new default commands", icon, StringComparison.Ordinal);
        Assert.DoesNotContain("Shellmason-Setup", icon, StringComparison.Ordinal);

        Assert.Contains("credential dialog", first, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("self-contained win-x64", first, StringComparison.Ordinal);
        Assert.Null(Changelog.Section(changelog, "0.9.0"));
        Assert.Null(Changelog.ReleaseNotes(changelog, "9.9.9"));
    }

    [Fact]
    public void Release_notes_are_the_section_plus_the_ownership_note()
    {
        var notes = Changelog.ReleaseNotes(ChangelogText(), "0.3.0");
        Assert.NotNull(notes);
        Assert.EndsWith(Changelog.OwnershipNote, notes, StringComparison.Ordinal);
        Assert.Contains("## 0.3.0", notes, StringComparison.Ordinal);
        Assert.DoesNotContain("## 0.2.0", notes, StringComparison.Ordinal);

        var prompt = Changelog.ForPrompt(notes, "0.3.0");
        Assert.Contains("115 commands", prompt, StringComparison.Ordinal);
        Assert.Contains(Changelog.OwnershipNote, prompt, StringComparison.Ordinal);
        Assert.Equal("No release notes were published.", Changelog.ForPrompt("  ", "0.3.0"));
    }

    [Fact]
    public void Release_workflow_fails_when_the_changelog_section_is_missing()
    {
        var root = Path.GetDirectoryName(Repo.CommandTreePath())!;
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));
        var script = File.ReadAllText(Path.Combine(root, "scripts", "New-ReleaseNotes.ps1"));

        Assert.Contains("./scripts/New-ReleaseNotes.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("CHANGELOG.md has no section for $version. The release was not published.", workflow, StringComparison.Ordinal);
        Assert.Contains("CHANGELOG.md has no section for $version. The release was not published.", script, StringComparison.Ordinal);
        Assert.Contains(Changelog.OwnershipNote, script, StringComparison.Ordinal);
        Assert.Contains("--notes-file $notesFile", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release", script, StringComparison.Ordinal);
    }
}
