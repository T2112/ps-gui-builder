// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.Cascade;
using PsGuiBuilder.Core.CommandTree;
using PsGuiBuilder.Tests.Support;

namespace PsGuiBuilder.Tests;

public class CascadeSessionTests
{
    private readonly CascadeSession _session = new(CommandTreeLoader.LoadFile(Repo.CommandTreePath()));

    [Fact]
    public void Starts_at_the_categories()
    {
        var level = Assert.Single(_session.Levels);
        Assert.Equal("I Want To:", level.Prompt);
        Assert.Equal(3, level.Options.Count);
        Assert.Null(_session.SelectedCommand);
    }

    [Fact]
    public void Filters_by_case_insensitive_tokens_without_selecting()
    {
        _session.SetFilter(0, "NETWORK drive");

        var filtered = _session.Levels[0].FilteredOptions;
        var match = Assert.Single(filtered);
        Assert.Equal("Map a Network Drive", match.Name);
        Assert.Null(_session.Levels[0].SelectedKey);
        Assert.Equal(3, _session.Levels[0].Options.Count);
    }

    [Fact]
    public void Empty_filter_shows_every_option()
    {
        _session.SetFilter(0, "map");
        _session.SetFilter(0, "  ");
        Assert.Equal(3, _session.Levels[0].FilteredOptions.Count);
    }

    [Fact]
    public void Selecting_a_branch_opens_the_next_level()
    {
        _session.Select(0, "Edit Exchange Settings");

        Assert.Equal(2, _session.Levels.Count);
        Assert.Equal("Then:", _session.Levels[1].Prompt);
        Assert.Equal(
            new[] { "On Exchange Online", "On a Network Server" },
            _session.Levels[1].Options.Select(option => option.Name).ToArray());
        Assert.Null(_session.SelectedCommand);
    }

    [Fact]
    public void Selecting_a_command_stops_the_cascade()
    {
        _session.Select(0, "Map a Network Drive");
        _session.Select(1, "Map New Drive");

        Assert.Equal(2, _session.Levels.Count);
        Assert.NotNull(_session.SelectedCommand);
        Assert.Equal("New-PSDrive", _session.SelectedCommand!.Cmdlet);
        Assert.False(_session.SelectedCommand.RequiresAdmin);
    }

    [Fact]
    public void Changing_an_earlier_choice_drops_later_levels()
    {
        _session.Select(0, "Edit Exchange Settings");
        _session.Select(1, "On Exchange Online");
        Assert.Equal(3, _session.Levels.Count);

        _session.Select(0, "Manage Users and Groups");

        Assert.Equal(2, _session.Levels.Count);
        Assert.Equal(
            new[]
            {
                "Add User to Group",
                "Add several users to a group",
                "Create a user account",
                "Find inactive accounts"
            },
            _session.Levels[1].Options.Select(option => option.Name).ToArray());
        Assert.Null(_session.SelectedCommand);
    }

    [Fact]
    public void Clear_selection_removes_the_command_and_deeper_levels()
    {
        _session.Select(0, "Map a Network Drive");
        _session.Select(1, "Map New Drive");

        _session.ClearSelection(1);

        Assert.Equal(2, _session.Levels.Count);
        Assert.Null(_session.Levels[1].SelectedKey);
        Assert.Null(_session.SelectedCommand);
    }

    [Fact]
    public void Try_resolve_accepts_an_exact_or_unique_match_only()
    {
        Assert.Equal("Map a Network Drive", _session.TryResolve(0, "  map a network drive "));
        Assert.Equal("Manage Users and Groups", _session.TryResolve(0, "groups"));
        Assert.Null(_session.TryResolve(0, "e"));
        Assert.Null(_session.TryResolve(0, ""));
    }

    [Fact]
    public void Cmdlet_text_is_searchable_on_a_command_level()
    {
        _session.Select(0, "Edit Exchange Settings");
        _session.Select(1, "On Exchange Online");
        _session.SetFilter(2, "remove-mailbox");

        var match = Assert.Single(_session.Levels[2].FilteredOptions);
        Assert.Equal("Remove Mailbox Folder Permission", match.Name);
        Assert.Contains("Remove-MailboxFolderPermission", match.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Typos_rank_the_closest_option_first()
    {
        _session.SetFilter(0, "exchnge");
        Assert.Equal("Edit Exchange Settings", _session.Levels[0].FilteredOptions[0].Name);

        _session.SetFilter(0, "netwrok drive");
        Assert.Equal("Map a Network Drive", Assert.Single(_session.Levels[0].FilteredOptions).Name);

        _session.SetFilter(0, "zzzzzz");
        Assert.Empty(_session.Levels[0].FilteredOptions);
    }

    [Fact]
    public void Unknown_option_throws()
    {
        var error = Assert.Throws<ArgumentException>(() => _session.Select(0, "Not a category"));
        Assert.Contains("not an option", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reset_returns_to_the_first_level()
    {
        _session.Select(0, "Map a Network Drive");
        _session.Reset();

        Assert.Single(_session.Levels);
        Assert.Null(_session.SelectedCommand);
        Assert.Equal("I Want To:", _session.Levels[0].Prompt);
    }
}
