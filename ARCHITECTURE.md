# Architecture Notes

Personal project — Thomas Schueler, personal time and equipment only. Personal side project. No employer resources were used.

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
- `command-tree.json` — the menu and parameter source of truth. An installed build copies that default into `%APPDATA%\PsGuiBuilder\command-tree.json` on first launch and loads the user copy after that, so an upgrade can replace the Program Files default without touching edits. `--tree` still wins. `PsGuiBuilder.installed` beside the executable marks an installed build; a dev build does not seed the user file.
- `VERSION` — the app and installer version, `major.minor.patch`.
- `installer/` — Inno Setup script and the self-contained win-x64 publish script.

## Elevated execution

`Process.Start` with the `runas` verb cannot redirect standard output, because `UseShellExecute` must be true. The window therefore stays at the user's normal integrity level and starts `PsGuiBuilder.ElevatedHost` elevated. A transcript or temp-file tail was set aside: it is slower, easier to leave behind, and worse at separating stdout from stderr. A named pipe keeps the stream live.

The desktop app creates the pipe before the helper starts. The access list allows the current user and Administrators. Messages are length-prefixed UTF-8 JSON: `script`, `stdout`, `stderr`, `exit`, `error`, `cancel`, and `cancelled`. Cancel is a message, because a non-elevated process cannot reliably kill an elevated one. If the user declines the UAC prompt, the run is treated as cancelled. The script is sent through the pipe, not placed on the command line.

The helper runs PowerShell 7 (`pwsh`) unless the command sets `"shell": "windows-powershell"`, which forces Windows PowerShell 5.1 for snap-ins such as on-premises Exchange. If `pwsh` is not installed, the run falls back to Windows PowerShell 5.1 and the output pane says so. `PSGUIBUILDER_SHELL` can point at a specific PowerShell 7 executable; it does not override a command that asks for 5.1. Captured stdout and stderr are decoded as UTF-16 LE when the bytes start with a BOM or when at least three quarters of the odd bytes are NUL, which is Windows PowerShell writing Unicode. Otherwise they are decoded as UTF-8, which covers `pwsh` and `powershell.exe` when the console code page is UTF-8. Script text sent to `powershell.exe` on standard input stays UTF-16. The script sets `$ErrorActionPreference = 'Stop'` and `$ProgressPreference = 'SilentlyContinue'`, and imports `module` when the command defines one.

Delivery is `-EncodedCommand`, or a temporary `.ps1` when that would be very long. A run that carries a secure string or a sign-in password does not use either of those. The secret is sent on the named pipe as its own field, then written only to the PowerShell process standard input as a `ConvertTo-SecureString` prologue. It is not placed on the command line, not written to a script file, and not copied into the preview or the output pane. The command text references `$__psgui_secret_<name>`. In-process runs put the same value in a `SecureString` variable and never add the characters to the script. Secrets shorter than 4 characters are still kept out of the preview and the script; they are not scrubbed from later cmdlet output, because a one-letter password would match unrelated text.

Non-elevated runs use the same process invoker without `runas`. **Run inside this app** uses the embedded PowerShell 7 engine instead. That mode cannot elevate, and it is disabled when the command asks for Windows PowerShell 5.1.

## Command tree and quoting

A node is either a branch (`options`) or a command (`cmdlet`), not both. Unknown properties are errors. Parameter `type` is `string`, `choice`, `switch`, `bool`, `int`, `number`, `securestring`, or `array`. Every parameter must appear in both the template and the sentence. Placeholders inside single quotes are errors, because PowerShell would not expand them. `switch`, `bool`, `int`, `number`, `securestring`, and `array` placeholders must sit outside double quotes, and a switch placeholder must be its own token. `choices` is valid on `choice` and `array`. A `securestring` cannot have a `default`, because that would store a password in the tree. Switch and bool defaults are the strings `"true"` or `"false"`.

`"shell"` is `pwsh` (the default) or `windows-powershell`. `"signIn"` is optional:

```json
{
  "service": "Exchange Online",
  "mode": "interactive",
  "prompt": "Sign in to Exchange Online. A browser window will open.",
  "command": "Connect-ExchangeOnline -ShowBanner:$false"
}
```

`mode` is `interactive` or `credential`. Interactive mode runs `command` in the same PowerShell process as the user's command, without `-NonInteractive`, so a module can open a browser. Credential mode asks for a user name and a masked password, builds `$__psgui_credential` from `$__psgui_username` and `$__psgui_secret_signin`, then runs `command`. The dialog starts empty on every run, including Run again. There is no "use the current user" choice, and the password is discarded when the run ends. Passwords are not written into `command-tree.json`, the preview, or the output log.

Windows are sequenced so only one is open. The credential dialog closes before UAC starts. An elevated interactive sign-in shares that PowerShell process, so Windows has to accept UAC before the process can open the Connect window; the app does not open the window until UAC has closed. While either sign-in window is open, the app does not open UAC, another dialog, or a result popup. The exit code is written to the output pane after that window closes. A parameter `securestring`, such as a new account password, stays in its masked field so Run again can reuse it. The sign-in password does not.

The live preview substitutes the template. Inside the author's double quotes, `` ` ``, `"`, `$`, and newlines are backtick-escaped so the value stays data. Outside quotes, a simple identifier is left bare (so `Editor` stays `Editor`) and anything else is wrapped in single quotes with embedded quotes doubled. `bool` becomes `$true` or `$false`. `int` and `number` are unquoted invariant-culture numbers. `array` is a comma-separated list of quoted items. `switch` becomes `-<name>` when on. A secure string is shown as `***` in the preview and as `$__psgui_secret_<name>` in the script that actually runs.

A blank optional parameter drops its flag and value when the placeholder is the whole argument (optionally in double quotes) or, for a switch, its own token. A blank value that is embedded in other template text, such as `{owner}:{folder}`, cannot be dropped; the form asks for a value. Required parameters are never dropped. An off switch is omitted even when it is required, and the form asks for it to be turned on, because writing the flag would turn the switch on.

Cascade search is fuzzy. The typed text is split on whitespace, every token must match the option name or description within a small edit distance (a transposition counts as one edit), and the matches are ordered by relevance. The first box is labeled "I Want To:" and later boxes "Then:". The tree does not define its own level labels.

Cmdlets that are not in the tree can be looked up by name. The app runs `Get-Command` and builds a mechanical sentence from the parameter metadata. Common parameters are skipped. Dynamic parameters are not included. Unrecognized .NET types become strings. `Get-Help` is not called, because help content may be missing and updating it can prompt or use the network. The looked-up command is not written back to `command-tree.json`.

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
2. `requiresAdmin` is a local UAC prompt. Service sign-in is the separate `signIn` object. The sample Exchange Online command calls `Connect-ExchangeOnline`; local elevation does not sign you in by itself.
3. These choices stay as they are: secrets shorter than 4 characters are not scrubbed from later cmdlet output; looked-up cmdlets skip dynamic parameters and `Get-Help`; a switch flag is `-{name}` using the PowerShell parameter name; a blank value embedded in other template text cannot be omitted; `number` is in the schema and is unused in the sample tree; **Run inside this app** stays off for Windows PowerShell 5.1; the Active Directory sample may import its module twice.
