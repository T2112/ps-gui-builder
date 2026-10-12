// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.CommandTree;
using PsGuiBuilder.Tests.Support;

namespace PsGuiBuilder.Tests;

public class CommandTreeLoaderTests
{
    [Fact]
    public void Loads_the_sample_command_tree()
    {
        var tree = CommandTreeLoader.LoadFile(Repo.CommandTreePath());

        Assert.Equal("0.1", tree.Version);
        Assert.Equal(
            new[] { "Edit Exchange Settings", "Map a Network Drive", "Manage Users and Groups" },
            tree.Root.Children.Select(child => child.Name).ToArray());

        var online = tree.Root.Children[0].Children[0];
        Assert.Equal("On Exchange Online", online.Name);
        var add = online.Children[0].Command;
        Assert.NotNull(add);
        Assert.Equal("Add-MailboxFolderPermission", add!.Cmdlet);
        Assert.Equal("ExchangeOnlineManagement", add.Module);
        Assert.True(add.RequiresAdmin);
        Assert.Equal(new[] { "owner", "folder", "delegate", "rights" }, add.Parameters.Select(parameter => parameter.Name).ToArray());
        Assert.Equal(ParameterType.Choice, add.Parameters[1].Type);
        Assert.Equal("Calendar", add.Parameters[1].Default);
        Assert.Contains("Editor", add.Parameters[3].Choices);
    }

    [Fact]
    public void Rejects_invalid_json()
    {
        var error = Assert.Throws<CommandTreeException>(() => CommandTreeLoader.Load("{"));
        Assert.Contains("JSON is not valid", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_an_empty_document()
    {
        var error = Assert.Throws<CommandTreeException>(() => CommandTreeLoader.Load("   "));
        Assert.Contains("empty", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_duplicate_keys()
    {
        const string json = """
        {
          "version": "1",
          "version": "2",
          "categories": {}
        }
        """;

        var error = Assert.Throws<CommandTreeException>(() => CommandTreeLoader.Load(json));
        Assert.Contains("Duplicate property \"version\"", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_unknown_properties_and_mixed_nodes()
    {
        const string json = """
        {
          "version": "1",
          "categories": {
            "Demo": {
              "summary": "nope",
              "options": {
                "Both": {
                  "options": { "Child": { "cmdlet": "Get-Item" } },
                  "cmdlet": "Get-Item"
                }
              }
            }
          }
        }
        """;

        var error = Assert.Throws<CommandTreeException>(() => CommandTreeLoader.Load(json));
        Assert.Contains("Unknown property \"summary\"", error.Message, StringComparison.Ordinal);
        Assert.Contains("not both", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_choice_parameter_whose_default_is_not_a_choice()
    {
        var error = Assert.Throws<CommandTreeException>(() => CommandTreeLoader.Load(Command(
            type: "choice",
            extra: """
            , "choices": ["A", "B"], "default": "C"
            """)));

        Assert.Contains("not one of the choices", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_choices_on_a_string_parameter()
    {
        var error = Assert.Throws<CommandTreeException>(() => CommandTreeLoader.Load(Command(
            type: "string",
            extra: ", \"choices\": [\"A\"]")));

        Assert.Contains("only valid when type is \"choice\"", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_unknown_parameter_types()
    {
        var error = Assert.Throws<CommandTreeException>(() => CommandTreeLoader.Load(Command(type: "int")));
        Assert.Contains("\"string\" or \"choice\"", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_parameter_missing_from_the_sentence()
    {
        const string json = """
        {
          "version": "1",
          "categories": {
            "Demo": {
              "options": {
                "Greet": {
                  "cmdlet": "Write-Output",
                  "template": "Write-Output \"{message}\" \"{extra}\"",
                  "sentence": "Say {message}",
                  "parameters": [
                    { "name": "message", "label": "message", "type": "string", "required": true },
                    { "name": "extra", "label": "extra", "type": "string", "required": false }
                  ]
                }
              }
            }
          }
        }
        """;

        var error = Assert.Throws<CommandTreeException>(() => CommandTreeLoader.Load(json));
        Assert.Contains("not used in the sentence", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_placeholder_inside_single_quotes()
    {
        const string json = """
        {
          "version": "1",
          "categories": {
            "Demo": {
              "options": {
                "Read": {
                  "cmdlet": "Get-Item",
                  "template": "Get-Item '{path}'",
                  "sentence": "Open {path}",
                  "parameters": [
                    { "name": "path", "label": "path", "type": "string", "required": true }
                  ]
                }
              }
            }
          }
        }
        """;

        var error = Assert.Throws<CommandTreeException>(() => CommandTreeLoader.Load(json));
        Assert.Contains("single quotes", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_an_unmatched_template_quote()
    {
        const string json = """
        {
          "version": "1",
          "categories": {
            "Demo": {
              "options": {
                "Read": {
                  "cmdlet": "Get-Item",
                  "template": "Get-Item \"{path}",
                  "sentence": "Open {path}",
                  "parameters": [
                    { "name": "path", "label": "path", "type": "string", "required": true }
                  ]
                }
              }
            }
          }
        }
        """;

        var error = Assert.Throws<CommandTreeException>(() => CommandTreeLoader.Load(json));
        Assert.Contains("unmatched quote", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_missing_file()
    {
        var error = Assert.Throws<CommandTreeException>(() => CommandTreeLoader.LoadFile(Path.Combine(Path.GetTempPath(), "missing-command-tree.json")));
        Assert.Contains("not found", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reports_more_than_one_problem()
    {
        const string json = """
        {
          "version": "1",
          "categories": {
            "Demo": {
              "options": {
                "Bad": {
                  "cmdlet": "not a cmdlet",
                  "requiresAdmin": "yes",
                  "template": "Get-Item {path}",
                  "sentence": "Open {path}",
                  "parameters": [
                    { "name": "path", "label": "path", "type": "string", "required": true },
                    { "name": "path", "label": "again", "type": "string", "required": true }
                  ]
                }
              }
            }
          }
        }
        """;

        var error = Assert.Throws<CommandTreeException>(() => CommandTreeLoader.Load(json));
        Assert.Contains("Verb-Noun", error.Message, StringComparison.Ordinal);
        Assert.Contains("requiresAdmin", error.Message, StringComparison.Ordinal);
        Assert.Contains("Duplicate parameter", error.Message, StringComparison.Ordinal);
    }

    private static string Command(string type, string extra = "") =>
        """
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
                    { "name": "message", "label": "message", "type": "TYPE", "required": true EXTRA }
                  ]
                }
              }
            }
          }
        }
        """.Replace("TYPE", type, StringComparison.Ordinal)
          .Replace("EXTRA", extra, StringComparison.Ordinal);
}
