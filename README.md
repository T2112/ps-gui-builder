# PowerShell GUI Command Builder

**Personal Project — Developed on Personal Time and Equipment**

This project is a personal side project created by Thomas Schueler (T2112 / thomas@schuelertech.com) entirely on personal time using personal equipment and resources. It is not related to any employment, has no connection to any employer, and no employer resources, time, code, or intellectual property were used in its creation or development.

All rights reserved to the author unless otherwise licensed.

## Purpose

A Windows GUI application that provides a guided, cascading searchable menu interface for building and executing PowerShell commands (including those requiring administrator privileges).

- Top area displays live PowerShell console output.
- Below: "I Want To:" searchable dropdown for high-level categories.
- Selecting an option dynamically adds the next searchable dropdown to the right.
- Continues until a complete cmdlet is selected.
- Parameter entry is presented as a natural-language sentence with input fields.
- Executes the filled command in an elevated PowerShell session and shows results.
- Offers to repeat the same action with new parameters or return to the start.

## Install

The installer is a self-contained 64-bit Windows app for .NET 8. The machine does not need the .NET runtime installed. PowerShell 7 is still the default host for the commands the app runs.

On Windows, with [Inno Setup 6](https://jrsoftware.org/isinfo.php):

```powershell
.\installer\build.ps1
```

That publishes `PsGuiBuilder` and `PsGuiBuilder.ElevatedHost` as self-contained `win-x64` builds, including `command-tree.json`, and writes `artifacts\PsGuiBuilder-Setup-<version>.exe` plus a `.sha256`. The version comes from the `VERSION` file.

The setup program shows the LICENSE, installs to `C:\Program Files\PsGuiBuilder`, registers an uninstaller, adds a Start menu shortcut, and offers an optional desktop shortcut. Publisher and copyright name Thomas Schueler, personal project.

The Program Files copy of `command-tree.json` is the default. The first launch of an installed build copies it to `%APPDATA%\PsGuiBuilder\command-tree.json` and uses that file from then on. Edit the AppData copy. An upgrade replaces the Program Files default and does not overwrite the AppData file. Delete the AppData file to seed a new copy from the upgraded default. `--tree path\to\command-tree.json` overrides both. A dev build does not create the AppData file unless it is already there.

Uninstall leaves the AppData command tree in place.

## Build and run

The desktop app is a Windows .NET 8 WPF program. The core tests also run on Linux.

Requirements:

- Windows 10 or later, for the WPF app and for elevated commands
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PowerShell 7](https://learn.microsoft.com/powershell/scripting/install/installing-powershell-on-windows) (`pwsh`), which is the default host. A command can ask for Windows PowerShell 5.1 instead. If `pwsh` is missing, the app falls back to Windows PowerShell 5.1 and says so.

From the repository root on Windows:

```powershell
dotnet test PsGuiBuilder.sln
dotnet run --project src/PsGuiBuilder
```

`dotnet test` builds the WPF app, the elevated helper, and the test project, then runs the tests. On Linux, build and run the core tests only:

```bash
dotnet test tests/PsGuiBuilder.Tests/PsGuiBuilder.Tests.csproj
```

The app loads `command-tree.json` from `%APPDATA%\PsGuiBuilder\` when that file exists, then from the build output, the current directory, or `--tree path\to\command-tree.json`.

Check **Run as administrator** to approve a UAC prompt and execute in an elevated PowerShell process. Leave it unchecked to run as the current user. **Run inside this app** uses the embedded PowerShell 7 engine instead of a separate process. That mode cannot elevate, and it cannot run a command that asks for Windows PowerShell 5.1. Commands can also ask for a service sign-in before they run. A credential sign-in opens an empty masked dialog on every run, including Run again. It does not offer the current Windows user and it does not reuse the previous password. While that dialog or a Connect window is open, the app does not open another window. Results are written to the output pane. Point `PSGUIBUILDER_SHELL` at a PowerShell 7 executable if you do not want the app to search for `pwsh`. A command that sets `"shell": "windows-powershell"` still uses Windows PowerShell 5.1.

The last box on the command row looks up any installed cmdlet with `Get-Command` when it is not in `command-tree.json`.

GitHub Actions builds, tests, and creates the installer on `windows-latest` for pull requests and for pushes to `main`. A second job runs the core tests on Linux. A manual run from `main`, or a `v*` tag, publishes a private GitHub Release `v<VERSION>` with `PsGuiBuilder-Setup-<version>.exe` and its `.sha256`. The repository stays private.

## Status

The .NET 8 WPF scaffold loads `command-tree.json`, searches through cascading boxes, fills a sentence-style parameter form, previews the quoted command, and can run it elevated or as the current user. The checked-in tree is still the sample. Add categories by editing `command-tree.json`.

## License

See LICENSE file. MIT License, Copyright Thomas Schueler. Personal side project, built on personal time with personal equipment. No employer resources were used.