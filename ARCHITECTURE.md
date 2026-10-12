# Architecture Notes

Personal project — Thomas Schueler, personal time and equipment only.

## Tech Stack Recommendation

- **.NET 8 WPF** application (C#) for the GUI.
  - Searchable cascading ComboBoxes (IsEditable + filtering or custom control).
  - Natural language parameter form generated from the command tree.
  - Embedded or separate PowerShell host using Microsoft.PowerShell.SDK / System.Management.Automation.
- Elevated execution: Application manifest `requireAdministrator`.
- Output: Capture and display in a scrollable console-like area at the top of the window.
- Alternative lighter start: Pure PowerShell + WPF (ShowUI or raw XAML), but C# is more maintainable for complex dynamic UI.

## Data-Driven Design

The `command-tree.json` drives the entire cascading menus and parameter forms. Adding new categories or cmdlets should not require UI code changes.

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

1. Create private GitHub repo under T2112 (e.g. `ps-gui-builder` or `powershell-command-builder`).
2. Scaffold .NET 8 WPF project.
3. Implement command tree loader and cascading ComboBox UI.
4. Implement parameter sentence form.
5. Implement elevated PowerShell execution and output capture.
6. Add more command categories incrementally.
