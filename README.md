# Shellmason

**Personal Project — Developed on Personal Time and Equipment**

This project is a personal side project created by Thomas Schueler (T2112 / thomas@schuelertech.com) entirely on personal time using personal equipment and resources. It is not related to any employment, has no connection to any employer, and no employer resources, time, code, or intellectual property were used in its creation or development.

All rights reserved to the author unless otherwise licensed.

## Purpose

Shellmason is a Windows GUI for building and running PowerShell commands, including commands that need administrator approval. The GitHub repository stays [T2112/ps-gui-builder](https://github.com/T2112/ps-gui-builder).

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

That publishes `PsGuiBuilder` and `PsGuiBuilder.ElevatedHost` as self-contained `win-x64` builds, including `command-tree.json`, and writes `artifacts\Shellmason-Setup-<version>.exe` plus a `.sha256`. The version comes from the `VERSION` file. The program file is still `PsGuiBuilder.exe`.

The setup program shows the LICENSE, installs a new copy to `C:\Program Files\Shellmason`, registers an uninstaller, adds a Start menu shortcut named Shellmason, and offers an optional desktop shortcut. An upgrade keeps the folder it was already installed in, because the installer id is unchanged. Publisher and copyright name Thomas Schueler, personal project.

The Program Files copy of `command-tree.json` is the default. The first launch copies it to `%APPDATA%\Shellmason\command-tree.json` when that file is not already there, and uses that file from then on. If `%APPDATA%\PsGuiBuilder` already has JSON files, the first launch copies the ones that are not in the Shellmason folder yet, including `command-tree.json`, and leaves the old folder in place. Edit the Shellmason copy. An upgrade replaces the Program Files default and does not overwrite the AppData file. **Load new default commands** adds commands that are in the new default and missing from your AppData copy. It leaves entries you already have, including edits, as they are. Delete the AppData file to seed a new copy from the upgraded default. `--tree path\to\command-tree.json` overrides both. A dev build does not create the AppData file unless it is already there.

On launch, and from Help > Check for updates, Shellmason asks `https://api.github.com/repos/T2112/ps-gui-builder/releases/latest` with no token. A newer published release offers to download `Shellmason-Setup-<version>.exe`, check its size and checksum, and run it. Later asks again next time. Skip this version stays quiet until a newer release. A copy started from source only points at the release page.

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

The app loads `command-tree.json` from `%APPDATA%\Shellmason\` when that file exists, then from the build output, the current directory, or `--tree path\to\command-tree.json`. On startup it copies JSON files from `%APPDATA%\PsGuiBuilder\` when the Shellmason copy is missing.

Check **Run as administrator** to approve a UAC prompt and execute in an elevated PowerShell process. Leave it unchecked to run as the current user. **Run inside this app** uses the embedded PowerShell 7 engine instead of a separate process. That mode cannot elevate, and it cannot run a command that asks for Windows PowerShell 5.1. Commands can also ask for a service sign-in before they run. A credential sign-in opens an empty masked dialog on every run, including Run again. It does not offer the current Windows user and it does not reuse the previous password. While that dialog or a Connect window is open, the app does not open another window. Results are written to the output pane. Point `PSGUIBUILDER_SHELL` at a PowerShell 7 executable if you do not want the app to search for `pwsh`. A command that sets `"shell": "windows-powershell"` still uses Windows PowerShell 5.1.

Favorites / Common is the curated `command-tree.json`. Installed commands lists modules and commands from `Get-Module -ListAvailable` and `Get-Command`, grouped by module or noun, with search and module, noun, and verb filters. Choosing one builds the sentence from its parameter sets, types, mandatory flags, `ValidateSet` choices, switches, arrays, and the `Get-Help` synopsis. The catalog is cached per shell, PowerShell 7 or Windows PowerShell 5.1, and Refresh reads the machine again. An optional pipeline step is appended to the command, for example `Select-Object Name | Out-GridView`.

If a command needs a module that is not installed, Shellmason says what it will install and waits for Yes. Gallery modules use `Install-Module -Scope CurrentUser`, including the NuGet provider and a temporary trust of PSGallery. RSAT modules such as ActiveDirectory use `Add-WindowsCapability` through the existing administrator prompt. Nothing is installed without that Yes.

GitHub Actions builds, tests, and creates the installer on `windows-latest` for pull requests and for pushes to `main`. A second job runs the core tests on Linux. A manual run from `main`, or a `v*` tag, publishes a normal GitHub Release `v<VERSION>` (not a draft and not a prerelease) with `Shellmason-Setup-<version>.exe` and its `.sha256`. The release notes are the matching `CHANGELOG.md` section plus the personal-ownership note. The release fails when that section is missing. That release is public when the repository is public. Pull requests do not publish a release.

## Status

The .NET 8 WPF app loads `command-tree.json`, searches through cascading boxes, fills a sentence-style parameter form, previews the quoted command, and can run it elevated or as the current user. The default tree covers files, processes, services, networking, disks, event logs, local accounts, scheduled tasks, the registry, Windows Update and system info, printers, Active Directory, Exchange Online, Microsoft Graph and Entra, Hyper-V, certificates, apps and packages, the firewall, and Windows features. Installed commands cover the rest of what that machine's PowerShell can see. Add more favorites by editing `command-tree.json`.

## License

See LICENSE file. MIT License, Copyright Thomas Schueler. Personal side project, built on personal time with personal equipment. No employer resources were used.