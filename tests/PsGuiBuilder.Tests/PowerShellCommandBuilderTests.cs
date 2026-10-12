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
        var preview = ScriptComposer.ComposePreview(command, result.CommandText);
        Assert.Contains("Import-Module ExchangeOnlineManagement", preview, StringComparison.Ordinal);
        Assert.Contains("Connect-ExchangeOnline -ShowBanner:$false", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("password", preview, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Builds_a_unc_path_without_eating_backslashes()
    {
        var command = Find("Map New Drive");
        var result = PowerShellCommandBuilder.Build(command, new Dictionary<string, string?>
        {
            ["driveLetter"] = "Z",
            ["uncPath"] = "\\\\server\\share",
            ["Persist"] = "true"
        });

        Assert.True(result.IsComplete);
        Assert.Equal(
            "New-PSDrive -Name \"Z\" -PSProvider FileSystem -Root \"\\\\server\\share\" -Persist",
            result.CommandText);
        Assert.DoesNotContain("Description", result.CommandText, StringComparison.Ordinal);
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
        Assert.Equal("Write-Output", empty.CommandText);
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
        var script = ScriptComposer.ComposeExecution(command, built.ExecutionText, "ada.ops");

        Assert.Contains("$ErrorActionPreference = 'Stop'", script, StringComparison.Ordinal);
        Assert.Contains("Import-Module ActiveDirectory", script, StringComparison.Ordinal);
        Assert.Contains("Add-ADGroupMember -Identity \"Ops\" -Members \"ada\" -Credential $__psgui_credential", script, StringComparison.Ordinal);
        Assert.Contains("$__psgui_username = 'ada.ops'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("S3cret", script, StringComparison.Ordinal);
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

    [Fact]
    public void Omits_a_blank_optional_flag_and_keeps_a_switch_that_is_on()
    {
        const string json = """
        {
          "version": "1",
          "categories": {
            "Demo": {
              "options": {
                "Run": {
                  "cmdlet": "Get-ChildItem",
                  "template": "Get-ChildItem -Path \"{path}\" -Filter \"{filter}\" {Recurse}",
                  "sentence": "List {path} matching {filter} recurse {Recurse}",
                  "parameters": [
                    { "name": "path", "label": "path", "type": "string", "required": true },
                    { "name": "filter", "label": "filter", "type": "string", "required": false },
                    { "name": "Recurse", "label": "recurse", "type": "switch", "required": false, "default": "false" }
                  ]
                }
              }
            }
          }
        }
        """;

        var command = CommandTreeLoader.Load(json).Root.Children[0].Children[0].Command!;
        var off = PowerShellCommandBuilder.Build(command, new Dictionary<string, string?>
        {
            ["path"] = "C:\\Temp",
            ["filter"] = "",
            ["Recurse"] = "false"
        });
        Assert.True(off.IsComplete);
        Assert.Equal("Get-ChildItem -Path \"C:\\Temp\"", off.CommandText);

        var on = PowerShellCommandBuilder.Build(command, new Dictionary<string, string?>
        {
            ["path"] = "C:\\Temp",
            ["filter"] = "*.log",
            ["Recurse"] = "true"
        });
        Assert.Equal("Get-ChildItem -Path \"C:\\Temp\" -Filter \"*.log\" -Recurse", on.CommandText);
    }

    [Fact]
    public void Renders_bool_int_number_and_array_values()
    {
        const string json = """
        {
          "version": "1",
          "categories": {
            "Demo": {
              "options": {
                "Run": {
                  "cmdlet": "Set-Item",
                  "template": "Set-Item -Enabled {enabled} -Count {count} -Ratio {ratio} -Name {names}",
                  "sentence": "Set {enabled} {count} {ratio} {names}",
                  "parameters": [
                    { "name": "enabled", "label": "enabled", "type": "bool", "required": true },
                    { "name": "count", "label": "count", "type": "int", "required": true },
                    { "name": "ratio", "label": "ratio", "type": "number", "required": false },
                    { "name": "names", "label": "names", "type": "array", "required": true }
                  ]
                }
              }
            }
          }
        }
        """;

        var command = CommandTreeLoader.Load(json).Root.Children[0].Children[0].Command!;
        var result = PowerShellCommandBuilder.Build(command, new Dictionary<string, ParameterValue>
        {
            ["enabled"] = ParameterValue.FromText("false"),
            ["count"] = ParameterValue.FromText("12"),
            ["ratio"] = ParameterValue.FromText(""),
            ["names"] = ParameterValue.FromItems(new[] { "ada", "O'Brien" })
        });

        Assert.True(result.IsComplete);
        Assert.Equal("Set-Item -Enabled $false -Count 12 -Name ada, 'O''Brien'", result.CommandText);
        Assert.Equal(result.CommandText, result.ExecutionText);
    }

    [Fact]
    public void A_blank_required_number_stays_in_the_preview()
    {
        const string json = """
        {
          "version": "1",
          "categories": {
            "Demo": {
              "options": {
                "Run": {
                  "cmdlet": "Set-Item",
                  "template": "Set-Item -Count {count}",
                  "sentence": "Set {count}",
                  "parameters": [
                    { "name": "count", "label": "count", "type": "int", "required": true }
                  ]
                }
              }
            }
          }
        }
        """;

        var command = CommandTreeLoader.Load(json).Root.Children[0].Children[0].Command!;
        var result = PowerShellCommandBuilder.Build(command, new Dictionary<string, string?> { ["count"] = "" });

        Assert.False(result.IsComplete);
        Assert.Contains("-Count", result.CommandText, StringComparison.Ordinal);
        Assert.Contains("count", Assert.Single(result.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Hides_a_secure_string_in_the_preview_and_the_execution_script()
    {
        const string json = """
        {
          "version": "1",
          "categories": {
            "Demo": {
              "options": {
                "Run": {
                  "cmdlet": "New-ADUser",
                  "template": "New-ADUser -Name \"{name}\" -AccountPassword {password}",
                  "sentence": "Create {name} with {password}",
                  "parameters": [
                    { "name": "name", "label": "name", "type": "string", "required": true },
                    { "name": "password", "label": "password", "type": "securestring", "required": true }
                  ]
                }
              }
            }
          }
        }
        """;

        var command = CommandTreeLoader.Load(json).Root.Children[0].Children[0].Command!;
        var result = PowerShellCommandBuilder.Build(command, new Dictionary<string, string?>
        {
            ["name"] = "Ada",
            ["password"] = "S3cret-Value!"
        });

        Assert.True(result.IsComplete);
        Assert.Equal("New-ADUser -Name \"Ada\" -AccountPassword ***", result.CommandText);
        Assert.Equal("New-ADUser -Name \"Ada\" -AccountPassword $__psgui_secret_password", result.ExecutionText);
        Assert.Equal("S3cret-Value!", result.Secrets["password"]);
        Assert.DoesNotContain("S3cret-Value!", result.CommandText, StringComparison.Ordinal);
        Assert.DoesNotContain("S3cret-Value!", result.ExecutionText, StringComparison.Ordinal);

        var script = ScriptComposer.ComposeExecution(command, result.ExecutionText);
        Assert.DoesNotContain("S3cret-Value!", script, StringComparison.Ordinal);
        Assert.Contains("$ErrorActionPreference = 'Stop'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_secure_string_default_and_a_switch_inside_quotes()
    {
        var stored = Assert.Throws<CommandTreeException>(() => CommandTreeLoader.Load("""
        {
          "version": "1",
          "categories": {
            "Demo": {
              "options": {
                "Run": {
                  "cmdlet": "New-ADUser",
                  "template": "New-ADUser -AccountPassword {password}",
                  "sentence": "Password {password}",
                  "parameters": [
                    { "name": "password", "label": "password", "type": "securestring", "required": true, "default": "hunter2" }
                  ]
                }
              }
            }
          }
        }
        """));
        Assert.Contains("cannot have a default", stored.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", stored.Message, StringComparison.Ordinal);

        var quoted = Assert.Throws<CommandTreeException>(() => CommandTreeLoader.Load("""
        {
          "version": "1",
          "categories": {
            "Demo": {
              "options": {
                "Run": {
                  "cmdlet": "Get-ChildItem",
                  "template": "Get-ChildItem \"{Recurse}\"",
                  "sentence": "Recurse {Recurse}",
                  "parameters": [
                    { "name": "Recurse", "label": "recurse", "type": "switch", "required": false }
                  ]
                }
              }
            }
          }
        }
        """));
        Assert.Contains("outside double quotes", quoted.Message, StringComparison.Ordinal);
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
