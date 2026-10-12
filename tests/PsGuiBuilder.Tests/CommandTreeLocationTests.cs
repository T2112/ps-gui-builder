// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Tests;

public class CommandTreeLocationTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "psgui-" + Guid.NewGuid().ToString("N"));
    private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);

    private string App => Path.Combine(_root, "app");

    private string UserTree => Path.Combine(_root, "appdata", CommandTreeLocations.UserFolderName, CommandTreeLocations.FileName);

    private string DefaultTree => Path.Combine(App, CommandTreeLocations.FileName);

    [Fact]
    public void Explicit_tree_wins_over_the_user_copy()
    {
        var requested = Path.Combine(_root, "custom.json");
        Add(requested);
        Add(UserTree);

        var resolved = Resolve(new[] { "--tree", requested });

        Assert.Equal(Path.GetFullPath(requested), resolved);
    }

    [Fact]
    public void Missing_explicit_tree_is_an_error()
    {
        var missing = Path.Combine(_root, "missing.json");
        var error = Assert.Throws<FileNotFoundException>(() => Resolve(new[] { "--TREE", missing }));
        Assert.Contains("not found", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tree_flag_requires_a_path()
    {
        var error = Assert.Throws<ArgumentException>(() => Resolve(new[] { "--tree" }));
        Assert.Contains("--tree", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void User_copy_is_preferred_and_an_upgrade_does_not_replace_it()
    {
        Add(UserTree);
        Add(DefaultTree);
        Add(Path.Combine(App, CommandTreeLocations.InstalledMarkerName));
        var copies = 0;

        var resolved = Resolve(Array.Empty<string>(), (source, destination) =>
        {
            copies++;
            Add(destination);
        });

        Assert.Equal(UserTree, resolved);
        Assert.Equal(0, copies);
    }

    [Fact]
    public void Installed_app_seeds_the_user_copy_once()
    {
        Add(DefaultTree);
        Add(Path.Combine(App, CommandTreeLocations.InstalledMarkerName));
        string? seeded = null;

        var resolved = Resolve(Array.Empty<string>(), (source, destination) =>
        {
            Assert.Equal(DefaultTree, source);
            seeded = destination;
            Add(destination);
        });

        Assert.Equal(UserTree, resolved);
        Assert.Equal(UserTree, seeded);
    }

    [Fact]
    public void A_failed_seed_falls_back_to_the_installed_default()
    {
        Add(DefaultTree);
        Add(Path.Combine(App, CommandTreeLocations.InstalledMarkerName));

        var resolved = Resolve(Array.Empty<string>(), (_, _) => throw new IOException("disk full"));

        Assert.Equal(DefaultTree, resolved);
    }

    [Fact]
    public void Dev_build_uses_the_copy_beside_the_app()
    {
        Add(DefaultTree);
        var copies = 0;

        var resolved = Resolve(Array.Empty<string>(), (_, _) => copies++);

        Assert.Equal(DefaultTree, resolved);
        Assert.Equal(0, copies);
    }

    [Fact]
    public void Falls_back_to_the_current_directory_and_then_parents()
    {
        var current = Path.Combine(_root, "work", CommandTreeLocations.FileName);
        Add(current);
        Assert.Equal(current, Resolve(Array.Empty<string>(), currentDirectory: Path.Combine(_root, "work")));

        _files.Remove(Path.GetFullPath(current));
        var parent = Path.Combine(_root, CommandTreeLocations.FileName);
        Add(parent);
        Assert.Equal(parent, Resolve(Array.Empty<string>()));
    }

    [Fact]
    public void Missing_tree_names_the_user_folder()
    {
        var error = Assert.Throws<FileNotFoundException>(() => Resolve(Array.Empty<string>()));
        Assert.Contains("%APPDATA%\\PsGuiBuilder\\", error.Message, StringComparison.Ordinal);
    }

    private string Resolve(
        IReadOnlyList<string> args,
        Action<string, string>? copy = null,
        string? currentDirectory = null) =>
        CommandTreeLocations.Resolve(
            args,
            Path.Combine(_root, "appdata"),
            App,
            currentDirectory ?? Path.Combine(_root, "work"),
            Exists,
            copy);

    private void Add(string path) => _files.Add(Path.GetFullPath(path));

    private bool Exists(string path) => _files.Contains(Path.GetFullPath(path));
}
