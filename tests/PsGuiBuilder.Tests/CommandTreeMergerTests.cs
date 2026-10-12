// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Tests;

public class CommandTreeMergerTests
{
    [Fact]
    public void Adds_missing_commands_and_keeps_edits()
    {
        const string user = """
        {
          "version": "0.1",
          "description": "my notes",
          "categories": {
            "Files and folders": {
              "description": "mine",
              "options": {
                "List a folder": {
                  "cmdlet": "Get-ChildItem",
                  "requiresAdmin": false,
                  "template": "Get-ChildItem -Path \"{path}\" -Filter \"{filter}\"",
                  "sentence": "I want {filter} in {path}",
                  "parameters": [
                    { "name": "path", "label": "folder", "type": "string", "required": true },
                    { "name": "filter", "label": "filter", "type": "string", "required": true }
                  ]
                },
                "My extra command": {
                  "cmdlet": "Get-Item",
                  "requiresAdmin": false,
                  "template": "Get-Item -Path \"{path}\"",
                  "sentence": "I want the item {path}",
                  "parameters": [
                    { "name": "path", "label": "path", "type": "string", "required": true }
                  ]
                }
              }
            }
          }
        }
        """;

        const string defaults = """
        {
          "version": "0.2",
          "description": "default",
          "categories": {
            "Files and folders": {
              "description": "replaced?",
              "options": {
                "List a folder": {
                  "cmdlet": "Get-ChildItem",
                  "requiresAdmin": false,
                  "template": "Get-ChildItem -Path \"{path}\"",
                  "sentence": "I want {path}",
                  "parameters": [
                    { "name": "path", "label": "folder", "type": "string", "required": true }
                  ]
                },
                "Create a folder": {
                  "cmdlet": "New-Item",
                  "requiresAdmin": false,
                  "template": "New-Item -Path \"{path}\" -ItemType Directory",
                  "sentence": "I want a folder at {path}",
                  "parameters": [
                    { "name": "path", "label": "folder", "type": "string", "required": true }
                  ]
                }
              }
            },
            "Processes": {
              "description": "processes",
              "options": {
                "List processes": {
                  "cmdlet": "Get-Process",
                  "requiresAdmin": false,
                  "template": "Get-Process -Name \"{name}\"",
                  "sentence": "I want processes named {name}",
                  "parameters": [
                    { "name": "name", "label": "name", "type": "string", "required": false }
                  ]
                }
              }
            }
          }
        }
        """;

        var merged = CommandTreeMerger.Merge(user, defaults);
        var tree = CommandTreeLoader.Load(merged.Json);

        Assert.Equal(2, merged.CommandsAdded);
        Assert.Equal(new[] { "Create a folder", "List processes" }, merged.AddedNames);
        Assert.Equal("0.1", tree.Version);
        Assert.Equal("my notes", tree.Description);

        var list = Find(tree.Root, "List a folder");
        Assert.NotNull(list);
        Assert.Contains("-Filter", list.Template, StringComparison.Ordinal);
        Assert.NotNull(Find(tree.Root, "My extra command"));
        Assert.NotNull(Find(tree.Root, "Create a folder"));
        Assert.NotNull(Find(tree.Root, "List processes"));
        Assert.Equal("mine", tree.Root.Children.Single(child => child.Name == "Files and folders").Description);
    }

    [Fact]
    public void Leaves_a_user_command_when_the_default_made_it_a_branch()
    {
        const string user = """
        {
          "version": "0.1",
          "categories": {
            "Tools": {
              "cmdlet": "Get-Date",
              "requiresAdmin": false,
              "template": "Get-Date",
              "sentence": "I want the date",
              "parameters": []
            }
          }
        }
        """;

        const string defaults = """
        {
          "version": "0.2",
          "categories": {
            "Tools": {
              "description": "several",
              "options": {
                "Clock": {
                  "cmdlet": "Get-Date",
                  "requiresAdmin": false,
                  "template": "Get-Date",
                  "sentence": "I want the clock",
                  "parameters": []
                }
              }
            }
          }
        }
        """;

        var merged = CommandTreeMerger.Merge(user, defaults);

        Assert.Equal(0, merged.CommandsAdded);
        var tree = CommandTreeLoader.Load(merged.Json);
        Assert.Equal("Get-Date", tree.Root.Children.Single().Command!.Cmdlet);
        Assert.Equal("I want the date", tree.Root.Children.Single().Command!.Sentence);
    }

    [Fact]
    public void Merge_files_writes_only_when_commands_were_added()
    {
        var dir = Path.Combine(Path.GetTempPath(), "psgui-merge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var userPath = Path.Combine(dir, "user.json");
            var defaultPath = Path.Combine(dir, "default.json");
            File.WriteAllText(userPath, """
            {
              "version": "0.1",
              "categories": {
                "Files and folders": {
                  "options": {
                    "List a folder": {
                      "cmdlet": "Get-ChildItem",
                      "requiresAdmin": false,
                      "template": "Get-ChildItem -Path \"{path}\"",
                      "sentence": "I want {path}",
                      "parameters": [
                        { "name": "path", "label": "folder", "type": "string", "required": true }
                      ]
                    }
                  }
                }
              }
            }
            """);
            var original = File.ReadAllText(userPath);
            File.WriteAllText(defaultPath, original);

            var same = CommandTreeMerger.MergeFiles(userPath, defaultPath);

            Assert.Equal(0, same.CommandsAdded);
            Assert.Equal(original, File.ReadAllText(userPath));

            File.WriteAllText(defaultPath, """
            {
              "version": "0.2",
              "categories": {
                "Files and folders": {
                  "options": {
                    "List a folder": {
                      "cmdlet": "Get-ChildItem",
                      "requiresAdmin": false,
                      "template": "Get-ChildItem -Path \"{path}\"",
                      "sentence": "I want {path}",
                      "parameters": [
                        { "name": "path", "label": "folder", "type": "string", "required": true }
                      ]
                    },
                    "Hash a file": {
                      "cmdlet": "Get-FileHash",
                      "requiresAdmin": false,
                      "template": "Get-FileHash -Path \"{path}\" -Algorithm {algorithm}",
                      "sentence": "I want the {algorithm} hash of {path}",
                      "parameters": [
                        { "name": "path", "label": "file", "type": "string", "required": true },
                        { "name": "algorithm", "label": "algorithm", "type": "choice", "required": true, "choices": ["SHA256"], "default": "SHA256" }
                      ]
                    }
                  }
                }
              }
            }
            """);

            var added = CommandTreeMerger.MergeFiles(userPath, defaultPath);
            var written = File.ReadAllText(userPath);

            Assert.Equal(1, added.CommandsAdded);
            Assert.Contains("Hash a file", written, StringComparison.Ordinal);
            Assert.Contains("-Path", written, StringComparison.Ordinal);
            CommandTreeLoader.Load(written);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static CommandDefinition? Find(CommandNode node, string name)
    {
        if (node.Command?.Name == name)
            return node.Command;

        foreach (var child in node.Children)
        {
            var found = Find(child, name);
            if (found is not null)
                return found;
        }

        return null;
    }
}
