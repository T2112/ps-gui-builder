// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.CommandTree;
using PsGuiBuilder.Tests.Support;

namespace PsGuiBuilder.Tests;

public class CommandTreeCatalogTests
{
    private readonly CommandTree _tree = CommandTreeLoader.LoadFile(Repo.CommandTreePath());

    [Fact]
    public void Default_tree_has_broad_categories_and_many_commands()
    {
        var categories = _tree.Root.Children.Select(child => child.Name).ToArray();
        Assert.Equal(
            new[]
            {
                "Files and folders",
                "Processes",
                "Services",
                "Networking",
                "Disks and storage",
                "Event logs",
                "Users and groups",
                "Scheduled tasks",
                "Registry",
                "Windows Update and system info",
                "Printers",
                "Active Directory",
                "Exchange Online",
                "Microsoft Graph and Entra",
                "Hyper-V"
            },
            categories);

        var commands = new List<CommandDefinition>();
        Collect(_tree.Root, commands);
        Assert.Equal(86, commands.Count);
        Assert.Equal(commands.Count, commands.Select(command => command.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("personal side project", _tree.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no employer resources", _tree.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Marks_admin_shell_and_sign_in_where_the_command_needs_them()
    {
        var stop = Find("Stop a service");
        Assert.True(stop.RequiresAdmin);
        Assert.Equal(PowerShellEdition.PowerShell7, stop.Shell);

        var classic = Find("Clear a classic log");
        Assert.True(classic.RequiresAdmin);
        Assert.Equal(PowerShellEdition.WindowsPowerShell5, classic.Shell);

        var onPrem = Find("Add Mailbox Folder Permission (On-Prem)");
        Assert.Equal(PowerShellEdition.WindowsPowerShell5, onPrem.Shell);
        Assert.Equal(SignInMode.Interactive, onPrem.SignIn!.Mode);

        var graph = Find("Get a Graph user");
        Assert.Equal("Microsoft Graph", graph.SignIn!.Service);
        Assert.Equal(SignInMode.Interactive, graph.SignIn.Mode);

        var ad = Find("Add User to Group");
        Assert.Equal(SignInMode.Credential, ad.SignIn!.Mode);
        Assert.True(ad.RequiresAdmin);

        var machine = Find("Create a virtual machine");
        Assert.True(machine.RequiresAdmin);
        Assert.Contains(machine.Parameters, parameter => parameter.Type == ParameterType.Number);
    }

    private CommandDefinition Find(string name)
    {
        CommandDefinition? found = null;
        void Walk(CommandNode node)
        {
            if (node.Command?.Name == name)
                found = node.Command;
            foreach (var child in node.Children)
                Walk(child);
        }

        Walk(_tree.Root);
        return found ?? throw new InvalidOperationException("Missing " + name);
    }

    private static void Collect(CommandNode node, List<CommandDefinition> commands)
    {
        if (node.Command is not null)
            commands.Add(node.Command);
        foreach (var child in node.Children)
            Collect(child, commands);
    }
}
