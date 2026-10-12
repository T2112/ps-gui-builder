// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.Building;
using PsGuiBuilder.Core.CommandTree;
using PsGuiBuilder.Core.Execution;
using PsGuiBuilder.Tests.Support;

namespace PsGuiBuilder.Tests;

public class PowerShellCommandBuilderTests
{
    [Fact]
    public void Builds_the_sample_exchange_command()
    {
        var command = Find("Add Mailbox Folder Permission");
        var result = PowerShellCommandBuilder.Build(command, new Dictionary<string, string?>
        {
            ["owner"] = "owner@yourdomain.com",
            ["folder"] = "Calendar",
            ["delegate"] = "delegate@yourdomain.com",
            ["rights"] = "Editor"
        });

        Assert.True(result.IsComplete);
        Assert.Equal(
            "Add-MailboxFolderPermission -Identity \"owner@yourdomain.com:Calendar\" -User \"delegate@yourdomain.com\" -AccessRights Editor",
            result.CommandText);
        Assert.StartsWith(
            "Import-Module ExchangeOnlineManagement" + Environment.NewLine,
            ScriptComposer.ComposePreview(command, result.CommandText),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Builds_a_unc_path_without_eating_backslashes()
    {
        var command = Find("Map New Drive");
        var result = PowerShellCommandBuilder.Build(command, new Dictionary<string, string?>
        {
            ["driveLetter"] = "Z",
            ["uncPath"] = "\\\\server\\share"
        });

        Assert.True(result.IsComplete);
        Assert.Equal(
            "New-PSDrive -Name \"Z\" -PSProvider FileSystem -Root \"\\\\server\\share\" -Persist",
            result.CommandText);
    }

    [Fact]
    public void Escapes_quotes_dollars_and_backticks_inside_double_quotes()
    {
        var command = Find("Add Mailbox Folder Permission");
        var result = PowerShellCommandBuilder.Build(command, new Dictionary<string, string?>
        {
            ["owner"] = "$(remove-item)",
            ["folder"] = "a`b",
            ["delegate"] = "bob\"",
            ["rights"] = "Editor"
        });

        Assert.Equal(
            "Add-MailboxFolderPermission -Identity \"`$(remove-item):a``b\" -User \"bob`\"\" -AccessRights Editor",
            result.CommandText);
    }

    [Fact]
    public void Quotes_bare_values_that_are_not_identifiers()
    {
        var command = LoadCommand("Get-Item", "Get-Item {path}", "Open {path}", "path");
        var result = PowerShellCommandBuilder.Build(command, new Dictionary<string, string?>
        {
            ["path"] = "O'Brien; Remove-Item C:\\"
        });

        Assert.Equal("Get-Item 'O''Brien; Remove-Item C:\\'", result.CommandText);
    }

    [Fact]
    public void Leaves_safe_bare_tokens_unquoted()
    {
        var command = LoadCommand("Show-Item", "Show-Item {name}", "Show {name}", "name");
        var result = PowerShellCommandBuilder.Build(command, new Dictionary<string, string?>
        {
            ["name"] = "Editor"
        });

        Assert.Equal("Show-Item Editor", result.CommandText);
    }

    [Fact]
    public void Escapes_newlines_inside_double_quotes()
    {
        var command = Find("Map New Drive");
        var result = PowerShellCommandBuilder.Build(command, new Dictionary<string, string?>
        {
            ["driveLetter"] = "Z",
            ["uncPath"] = "one\ntwo"
        });

        Assert.Contains("-Root \"one`ntwo\"", result.CommandText, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_required_values_are_reported_and_still_previewed()
    {
        var command = Find("Add User to Group");
        var result = PowerShellCommandBuilder.Build(command, new Dictionary<string, string?>
        {
            ["user"] = "   ",
            ["group"] = "Helpdesk"
        });

        Assert.False(result.IsComplete);
        Assert.Contains("user", Assert.Single(result.Errors), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Helpdesk", result.CommandText, StringComparison.Ordinal);
    }

    [Fact]
    public void Blank_optional_values_are_complete()
    {
        const string json = """
        {
          "version": "1",
          "categories": {
            "Demo": {
              "options": {
                "Greet": {
                  "cmdlet": "Write-Output",
                  "template": "Write-Output \"{message}\"",
                  "sentence": "Say {message}",
                  "parameters": [
                    { "name": "message", "label": "message", "type": "string", "required": false, "default": "hello" }
                  ]
                }
              }
            }
          }
        }
        """;

        var command = CommandTreeLoader.Load(json).Root.Children[0].Children[0].Command!;
        var seeded = ParameterValues.Seed(command);
        Assert.Equal("hello", seeded["message"]);

        var empty = PowerShellCommandBuilder.Build(command, new Dictionary<string, string?> { ["message"] = "" });
        Assert.True(empty.IsComplete);
        Assert.Equal("Write-Output \"\"", empty.CommandText);
    }

    [Fact]
    public void Rejects_a_choice_outside_the_list()
    {
        var command = Find("Add Mailbox Folder Permission");
        var values = ParameterValues.Seed(command).ToDictionary(pair => pair.Key, pair => (string?)pair.Value);
        values["owner"] = "owner@yourdomain.com";
        values["delegate"] = "delegate@yourdomain.com";
        values["rights"] = "Everything";

        var result = PowerShellCommandBuilder.Build(command, values);
        Assert.Contains("permission level", Assert.Single(result.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sentence_segments_follow_the_sample_wording()
    {
        var form = SentenceForm.Parse(Find("Add Mailbox Folder Permission"));
        Assert.Equal(
            new[] { "text", "delegate", "text", "rights", "text", "owner", "text", "folder" },
            form.Segments.Select(Describe).ToArray());
        Assert.Equal("I want to grant ", ((SentenceSegment.Text)form.Segments[0]).Value);
        Assert.Equal("'s ", ((SentenceSegment.Text)form.Segments[6]).Value);
    }

    [Fact]
    public void Execution_script_stops_on_errors_and_roundtrips_through_encoded_command()
    {
        var command = Find("Add User to Group");
        var built = PowerShellCommandBuilder.Build(command, new Dictionary<string, string?>
        {
            ["user"] = "ada",
            ["group"] = "Ops"
        });
        var script = ScriptComposer.ComposeExecution(command, built.CommandText);

        Assert.Contains("$ErrorActionPreference = 'Stop'", script, StringComparison.Ordinal);
        Assert.Contains("Import-Module ActiveDirectory", script, StringComparison.Ordinal);
        Assert.Contains("Add-ADGroupMember -Identity \"Ops\" -Members \"ada\"", script, StringComparison.Ordinal);
        Assert.Equal(script, PowerShellCommandEncoding.FromEncodedCommand(PowerShellCommandEncoding.ToEncodedCommand(script)));
    }

    [Fact]
    public void Windows_powershell_uses_unicode_and_pwsh_uses_utf8()
    {
        var windows = PowerShellShell.ForExecutable("powershell.exe");
        var modern = PowerShellShell.ForExecutable("pwsh");

        Assert.Equal(ScriptDelivery.EncodedCommand, windows.Delivery);
        Assert.Equal(System.Text.Encoding.Unicode, windows.Encoding);
        Assert.Empty(modern.Encoding.GetPreamble());
        Assert.NotEqual(windows.Encoding.WebName, modern.Encoding.WebName);
    }

    private static CommandDefinition Find(string name)
    {
        var tree = CommandTreeLoader.LoadFile(Repo.CommandTreePath());
        return Find(tree.Root, name) ?? throw new InvalidOperationException($"Missing sample command {name}.");
    }

    private static CommandDefinition? Find(CommandNode node, string name)
    {
        if (node.Command is not null && node.Name == name)
            return node.Command;

        foreach (var child in node.Children)
        {
            var found = Find(child, name);
            if (found is not null)
                return found;
        }

        return null;
    }

    private static CommandDefinition LoadCommand(string cmdlet, string template, string sentence, string parameterName)
    {
        var json = """
        {
          "version": "1",
          "categories": {
            "Demo": {
              "options": {
                "Run": {
                  "cmdlet": "CMDLET",
                  "template": "TEMPLATE",
                  "sentence": "SENTENCE",
                  "parameters": [
                    { "name": "NAME", "label": "NAME", "type": "string", "required": true }
                  ]
                }
              }
            }
          }
        }
        """.Replace("CMDLET", cmdlet, StringComparison.Ordinal)
          .Replace("TEMPLATE", template, StringComparison.Ordinal)
          .Replace("SENTENCE", sentence, StringComparison.Ordinal)
          .Replace("NAME", parameterName, StringComparison.Ordinal);

        return CommandTreeLoader.Load(json).Root.Children[0].Children[0].Command!;
    }

    private static string Describe(SentenceSegment segment) => segment switch
    {
        SentenceSegment.Text => "text",
        SentenceSegment.Field field => field.Parameter.Name,
        _ => "other"
    };
}
