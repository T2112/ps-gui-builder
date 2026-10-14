# Changelog

Versions follow the `VERSION` file. There are no git tags for these releases. Each section is the change that landed while that version was current.

## 0.3.0

- The product is named Shellmason. The installer file is `Shellmason-Setup-<version>.exe`, the Start menu shortcut is Shellmason, and the program file is still `PsGuiBuilder.exe`. The installer id is unchanged, so an upgrade keeps the folder it was already installed in.
- The first launch copies JSON files from `%APPDATA%\PsGuiBuilder` into `%APPDATA%\Shellmason` when the Shellmason copy is missing, and leaves the old folder in place.
- Favorites / Common stays the curated `command-tree.json`. Installed commands lists modules and commands from `Get-Module -ListAvailable` and `Get-Command`, grouped by module or noun, with search and module, noun, and verb filters. The catalog is cached per shell, PowerShell 7 or Windows PowerShell 5.1.
- Choosing an installed command builds the sentence from its parameter sets, types, mandatory flags, `ValidateSet` choices, switches, arrays, and the `Get-Help` synopsis. An optional pipeline step can be appended.
- If a command needs a module that is not installed, Shellmason says what it will install and waits for Yes. Gallery modules use `Install-Module -Scope CurrentUser`. RSAT modules use `Add-WindowsCapability`. Hyper-V uses `Enable-WindowsOptionalFeature`.
- On launch, and from Help > Check for updates, the app reads the latest GitHub release with no token. A newer release can download `Shellmason-Setup-<version>.exe`, check its size and checksum, and run it. Later asks again next time. Skip this version stays quiet until a newer release.
- The default command tree has 19 categories and 115 commands.
- A manual run from `main`, or a `v*` tag, publishes a normal GitHub Release, not a draft and not a prerelease.

## 0.2.0

- The window, the program, and the installer use an app icon.
- The default command tree has 15 categories and 86 commands.
- Load new default commands adds commands that are in the new default and missing from the AppData copy. It leaves entries you already have, including edits, as they are.

## 0.1.0

- A .NET 8 WPF app loads `command-tree.json`, cascades searchable choices into a sentence form, previews a quoted PowerShell command, and runs it through an elevated helper.
- Parameter types include switch, bool, int, number, secure string, and array. Blank optional flags are omitted. Search is fuzzy.
- Runs default to PowerShell 7, with a per-command Windows PowerShell 5.1 option and a clear fallback when `pwsh` is missing.
- A command can require a service sign-in without storing the password. The credential dialog starts empty on every run, the password is discarded when the run ends, and other windows wait until that dialog closes.
- The Inno Setup installer is a self-contained win-x64 publish with the elevated helper. It shows the LICENSE, installs to Program Files, and leaves the per-user command tree in `%APPDATA%` alone on upgrade.
- The published app copies the elevated helper from its win-x64 output. Windows CI reads PowerShell pipe output with the encoding the process used.
