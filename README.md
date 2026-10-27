# Shellmason

Shellmason builds a PowerShell command from cascading menus and a fill-in sentence, then runs it and shows the output in the same window.

[![CI](https://img.shields.io/github/actions/workflow/status/T2112/ps-gui-builder/ci.yml?branch=main&label=CI)](https://github.com/T2112/ps-gui-builder/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/T2112/ps-gui-builder?label=release)](https://github.com/T2112/ps-gui-builder/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/T2112/ps-gui-builder/total?label=downloads)](https://github.com/T2112/ps-gui-builder/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Personal project by Thomas Schueler, built on personal time and equipment. See [OWNERSHIP.md](OWNERSHIP.md).

Shellmason (repository: ps-gui-builder, executable: PsGuiBuilder.exe). The repository stays [T2112/ps-gui-builder](https://github.com/T2112/ps-gui-builder).

## What it is

Shellmason is a Windows desktop app for people who know the task and want the command written, quoted, and run. You walk a set of menus, fill a sentence, and read the console output at the top of the window. Commands that need administrator approval go through a UAC prompt.

The curated tree has 19 categories and 115 commands: files, processes, services, networking, disks, event logs, local accounts, scheduled tasks, the registry, Windows Update and system info, printers, Active Directory, Exchange Online, Microsoft Graph and Entra, Hyper-V, certificates, apps and packages, the firewall, and Windows features. Installed commands cover the other cmdlets that machine's PowerShell can see. Add more favorites by editing the command tree.

## Key features

- Searchable cascading menus. The first box is **I Want To:** and the next boxes are **Then:**, until a command is selected.
- A sentence-style parameter form, with a quoted PowerShell preview before the command runs.
- Run as the current user, elevated after UAC, or inside the app with the embedded PowerShell 7 engine.
- An installed-command browser with search and module, noun, and verb filters. The form is built from that command's parameter sets.
- A confirmation before a missing module is installed.
- An update check against the latest GitHub release. The download is checked against the published size and checksum.
- A per-user command tree that upgrades do not overwrite.

## Screenshots

Pictures of the cascading menus, the sentence-style form, and the output pane are not in this repository yet. Shellmason is a Windows Presentation Foundation app, so the window was not captured here. No stand-in image is included.

The shots to take, and the file names to use, are in [docs/screenshots/README.md](docs/screenshots/README.md):

- `docs/screenshots/cascading-menus.png` — **I Want To:** and **Then:**, with the second menu open
- `docs/screenshots/sentence-form.png` — the **List a folder** sentence and the PowerShell preview
- `docs/screenshots/output-pane.png` — the **Output** pane after that command runs

An optional GIF of the same path can be saved as `docs/screenshots/cascade-to-output.gif`. After the files exist, embed them in this section.

## Install

Download the latest installer from [the GitHub releases page](https://github.com/T2112/ps-gui-builder/releases/latest). The asset is `Shellmason-Setup-<version>.exe`, with a `.sha256` file beside it. Windows 10 or later, 64-bit, is required. The installed app does not need the .NET runtime. PowerShell 7 is the default host for the commands it runs. If `pwsh` is missing, Shellmason falls back to Windows PowerShell 5.1 and says so.

The installer is not Authenticode-signed. Windows SmartScreen may say that it protected your PC and that the publisher is unrecognized. That is the usual prompt for an unsigned download that Windows has not seen often. If you trust this repository, choose **More info**, then **Run anyway**. To check the file first, compare `Get-FileHash` of the `.exe` (SHA256) with the published `.sha256` file.

The setup program shows [OWNERSHIP.md](OWNERSHIP.md), then [LICENSE](LICENSE). It installs a new copy to `C:\Program Files\Shellmason`, registers an uninstaller, adds a Start menu shortcut named Shellmason, and offers an optional desktop shortcut. An upgrade keeps the folder it was already installed in, because the installer id is unchanged. Publisher and copyright name Thomas Schueler, personal project.

The Program Files copy of `command-tree.json` is the default. The first launch copies it to `%APPDATA%\Shellmason\command-tree.json` when that file is not already there, and uses that file from then on. If `%APPDATA%\PsGuiBuilder` already has JSON files, the first launch copies the ones that are not in the Shellmason folder yet, including `command-tree.json`, and leaves the old folder in place. Edit the Shellmason copy. An upgrade replaces the Program Files default and does not overwrite the AppData file. **Load new default commands** adds commands that are in the new default and missing from your AppData copy. It leaves entries you already have, including edits, as they are. Delete the AppData file to seed a new copy from the upgraded default. `--tree path\to\command-tree.json` overrides both. A dev build does not create the AppData file unless it is already there.

On launch, and from Help > Check for updates, Shellmason asks `https://api.github.com/repos/T2112/ps-gui-builder/releases/latest` with no token. A newer published release offers to download `Shellmason-Setup-<version>.exe`, check its size and checksum, and run it. Later asks again next time. Skip this version stays quiet until a newer release. A copy started from source only points at the release page.

Uninstall leaves the AppData command tree in place.

## Usage

Leave **Favorites / Common** selected, or switch to **Installed commands**. In Favorites, choose **I Want To:** and then each **Then:** box until a command is selected. You can also type a cmdlet name and choose **Look up**.

The Parameters card becomes a sentence with fields for the values. Switches, choices, numbers, arrays, and masked secrets are filled there. The PowerShell card shows the quoted command. An optional pipeline step is appended, for example `Select-Object Name | Out-GridView`.

**Run** executes it. Check **Run as administrator** to approve a UAC prompt and run in an elevated PowerShell process. Leave it unchecked to run as the current user. **Run inside this app** uses the embedded PowerShell 7 engine instead of a separate process. That mode cannot elevate, and it cannot run a command that asks for Windows PowerShell 5.1. **Run again** repeats the same action with new parameters. **Start over** returns to the first menu.

Results are written to the output pane. Point `PSGUIBUILDER_SHELL` at a PowerShell 7 executable if you do not want Shellmason to search for `pwsh`. A command that sets `"shell": "windows-powershell"` still uses Windows PowerShell 5.1.

A command can ask for a service sign-in before it runs. A credential sign-in opens an empty masked dialog on every run, including Run again. It does not offer the current Windows user and it does not reuse the previous password. While that dialog or a Connect window is open, Shellmason does not open another window.

If a command needs a module that is not installed, Shellmason says what it will install and waits for Yes. Gallery modules use `Install-Module -Scope CurrentUser`, including the NuGet provider and a temporary trust of PSGallery that is set back afterward. RSAT modules such as ActiveDirectory use `Add-WindowsCapability` through the administrator prompt. Hyper-V uses `Enable-WindowsOptionalFeature`. Nothing is installed without that Yes.

## Tech stack

- C# and .NET 8. The desktop window is WPF (`net8.0-windows`).
- PowerShell 7 (`pwsh`) is the default host. Windows PowerShell 5.1 is used when a command asks for it, or when `pwsh` is not installed.
- In-process runs use Microsoft.PowerShell.SDK 7.4.
- The installer is Inno Setup 6. It packages a self-contained win-x64 publish, so the machine does not need a separate .NET runtime.
- GitHub Actions builds, tests, and creates the installer on Windows. A second job runs the core tests on Linux.

## Architecture

The long version is [ARCHITECTURE.md](ARCHITECTURE.md). What the code does:

- `command-tree.json` drives the menus and the sentence. Adding a favorite does not require a UI change. Menu search is fuzzy: each typed word must match within a small edit distance, and a transposition counts as one edit.
- The window stays `asInvoker`. A command that needs a local administrator starts `PsGuiBuilder.ElevatedHost` with the `runas` verb. Console output comes back on a named pipe whose access list is the current user and Administrators. The script is sent through the pipe, not placed on the command line.
- A secret is shown as `***` in the preview. It is not written into the command tree, the preview, or a script file. It is handed to the PowerShell process on standard input as a `ConvertTo-SecureString` prologue. The credential dialog starts empty on every run, and that password is discarded when the run ends.
- The first launch copies JSON from `%APPDATA%\PsGuiBuilder` into `%APPDATA%\Shellmason` when the new file is missing, and leaves the old folder. **Load new default commands** adds entries that are in the new default and missing from the user file.
- Installed commands come from `Get-Module -ListAvailable` and `Get-Command`, cached per shell (PowerShell 7 or Windows PowerShell 5.1). The form uses parameter sets, types, mandatory flags, `ValidateSet` choices, and the `Get-Help` synopsis. Shellmason does not call `Update-Help`.
- A manual run from `main`, or a `v*` tag, publishes a normal GitHub Release (not a draft and not a prerelease) with `Shellmason-Setup-<version>.exe` and its `.sha256`. The release body is the matching `CHANGELOG.md` section plus the personal-ownership note, written by [`scripts/New-ReleaseNotes.ps1`](scripts/New-ReleaseNotes.ps1). The job stops, and does not publish, when that section is missing. Pull requests do not publish a release.

## Changelog and issues

- [CHANGELOG.md](CHANGELOG.md) — what changed in each version
- [Issues](https://github.com/T2112/ps-gui-builder/issues) — bug reports and feature requests
- [Security](SECURITY.md) — how to report a vulnerability
- [Showcase copy](docs/showcase.md) — portfolio, LinkedIn, and GitHub profile text

## Build from source

The desktop app is a Windows .NET 8 WPF program. The core tests also run on Linux.

Requirements:

- Windows 10 or later, for the WPF app and for elevated commands
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PowerShell 7](https://learn.microsoft.com/powershell/scripting/install/installing-powershell-on-windows) (`pwsh`)
- [Inno Setup 6](https://jrsoftware.org/isinfo.php), only if you are building the installer

From the repository root on Windows:

```powershell
dotnet test PsGuiBuilder.sln
dotnet run --project src/PsGuiBuilder
.\installer\build.ps1
```

`dotnet test` builds the WPF app, the elevated helper, and the test project, then runs the tests. On Linux, build and run the core tests only:

```bash
dotnet test tests/PsGuiBuilder.Tests/PsGuiBuilder.Tests.csproj
```

`installer\build.ps1` publishes a self-contained win-x64 build, including `command-tree.json`, and writes `artifacts\Shellmason-Setup-<version>.exe` plus a `.sha256`. The version comes from the `VERSION` file.

A dev build loads `command-tree.json` from `%APPDATA%\Shellmason\` when that file exists, then from the build output, the current directory, or `--tree`. It does not seed the AppData file unless that file is already there.

## Ownership

**Personal Project — Developed on Personal Time and Equipment**

This project is a personal side project created by Thomas Schueler (T2112 / thomas@schuelertech.com) entirely on personal time using personal equipment and resources. It is not related to any employment, has no connection to any employer, and no employer resources, time, code, or intellectual property were used in its creation or development.

All rights reserved to the author unless otherwise licensed.

## License

[LICENSE](LICENSE) is the MIT License. Copyright Thomas Schueler. The file is the standard MIT text so GitHub can identify it. Personal side project, built on personal time with personal equipment. No employer resources were used. The personal-development statement that used to follow the MIT text is kept in full in [OWNERSHIP.md](OWNERSHIP.md).
