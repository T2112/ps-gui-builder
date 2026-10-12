# Architecture Notes

Personal project — Thomas Schueler, personal time and equipment only.

## Tech Stack Recommendation

- **.NET 8 WPF** application (C#) for the GUI.
  - Searchable cascading ComboBoxes (IsEditable + filtering or custom control).
  - Natural language parameter form generated from the command tree.
  - Embedded or separate PowerShell host using Microsoft.PowerShell.SDK / System.Management.Automation.
- Elevated execution: per command, by launching `PsGuiBuilder.ElevatedHost` with the `runas` verb. The application manifest stays `asInvoker`. See Elevated execution below.
- Output: Capture and display in a scrollable console-like area at the top of the window.
- Non-elevated commands can run in a separate PowerShell process, or in-process with `Microsoft.PowerShell.SDK` 7.4.
- Alternative lighter start: Pure PowerShell + WPF (ShowUI or raw XAML), but C# is more maintainable for complex dynamic UI.

## Data-Driven Design

The `command-tree.json` drives the entire cascading menus and parameter forms. Adding new categories or cmdlets should not require UI code changes.

## Project layout

- `src/PsGuiBuilder.Core` — command tree, cascade filtering, quoting, and the execution protocol. No WPF types, so the tests run on Linux.
- `src/PsGuiBuilder` — WPF window, view models, and the elevated-pipe parent.
- `src/PsGuiBuilder.ElevatedHost` — small helper started with `runas`. It only receives a pipe name on the command line.
- `src/PsGuiBuilder.PowerShell` — optional in-process PowerShell 7 host for non-elevated runs.
- `tests/PsGuiBuilder.Tests` — loader, cascade, quoting, and protocol tests.
- `command-tree.json` — the menu and parameter source of truth.

## Elevated execution

`Process.Start` with the `runas` verb cannot redirect standard output, because `UseShellExecute` must be true. The window therefore stays at the user's normal integrity level and starts `PsGuiBuilder.ElevatedHost` elevated. A transcript or temp-file tail was set aside: it is slower, easier to leave behind, and worse at separating stdout from stderr. A named pipe keeps the stream live.

The desktop app creates the pipe before the helper starts. The access list allows the current user and Administrators. Messages are length-prefixed UTF-8 JSON: `script`, `stdout`, `stderr`, `exit`, `error`, `cancel`, and `cancelled`. Cancel is a message, because a non-elevated process cannot reliably kill an elevated one. If the user declines the UAC prompt, the run is treated as cancelled. The script is sent through the pipe, not placed on the command line.

The helper runs Windows PowerShell 5.1 (`powershell.exe`) with `-EncodedCommand`, unless the script is very long, in which case it uses a temporary `.ps1` file that is deleted afterward. Set `PSGUIBUILDER_SHELL` to point at `pwsh` or another host. Standard output from `powershell.exe` is decoded as UTF-16; `pwsh` is decoded as UTF-8. The script sets `$ErrorActionPreference = 'Stop'` and `$ProgressPreference = 'SilentlyContinue'`, and imports `module` when the command defines one.

Non-elevated runs use the same process invoker without `runas`. **Run inside this app** uses the embedded PowerShell 7 engine instead. That mode cannot elevate, and it may not see Windows PowerShell 5.1 modules such as ActiveDirectory or ExchangeOnlineManagement.

## Command tree and quoting

A node is either a branch (`options`) or a command (`cmdlet`), not both. Unknown properties are errors. Parameter `type` is `string` or `choice`. Every parameter must appear in both the template and the sentence. Placeholders inside single quotes are errors, because PowerShell would not expand them.

The live preview substitutes the template. Inside the author's double quotes, `` ` ``, `"`, `$`, and newlines are backtick-escaped so the value stays data. Outside quotes, a simple identifier is left bare (so `Editor` stays `Editor`) and anything else is wrapped in single quotes with embedded quotes doubled. Optional parameters are not omitted when blank; the template is one string, so a blank value is inserted as an empty quoted string. The sample tree has no optional parameters.

Cascade search splits the typed text on whitespace. Every token must appear in the option name or its description (command options include the cmdlet and module). The first box is labeled "I Want To:" and later boxes "Then:". The tree does not define its own level labels.

## Ownership Logging

- README and LICENSE explicitly state personal development.
- All source files will include a header comment:
  ```
  // Personal project by Thomas Schueler.
  // Developed on personal time using personal equipment.
  // Not related to any employment.
  ```
- Git commits will reference personal development where appropriate.
- No work performed on employer systems, networks, or during work hours.

## Next Steps

1. Add more categories and cmdlets to `command-tree.json`.
2. Decide whether a blank optional parameter should omit its flag. The template would need a way to mark an optional clause.
3. Decide whether elevated commands should keep using Windows PowerShell 5.1, which matches the Exchange and Active Directory modules in the sample, or move to PowerShell 7.
4. Decide whether `requiresAdmin` means a local UAC prompt, an Exchange or directory role, or both. The app currently treats it as a local UAC prompt. The sample templates do not call `Connect-ExchangeOnline`.
