; Personal project by Thomas Schueler.
; Developed on personal time using personal equipment.
; Not related to any employment.

#ifndef AppVersion
  #error AppVersion must be passed as /DAppVersion=x.y.z from the VERSION file.
#endif
#ifndef PublishDir
  #error PublishDir must be the self-contained win-x64 publish folder.
#endif
#ifndef RepoRoot
  #error RepoRoot must be the repository root, so the installer can show LICENSE.
#endif

#define AppName "PowerShell GUI Command Builder"
#define Publisher "Thomas Schueler"
#define Copyright "Copyright (c) 2026 Thomas Schueler. Personal project, developed on personal time using personal equipment. Not related to any employment."

[Setup]
AppId={{E4B7C2A1-9F30-4D6E-8A12-5C7B9E0D3F48}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#Publisher}
AppCopyright={#Copyright}
DefaultDirName={autopf}\PsGuiBuilder
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile={#RepoRoot}\LICENSE
OutputDir={#RepoRoot}\artifacts
OutputBaseFilename=PsGuiBuilder-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\PsGuiBuilder.exe
VersionInfoCompany={#Publisher}
VersionInfoCopyright={#Copyright}
VersionInfoDescription=Personal PowerShell GUI Command Builder by Thomas Schueler. Not related to any employment.
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
VersionInfoVersion={#AppVersion}.0
MinVersion=10.0

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\PsGuiBuilder.exe"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\PsGuiBuilder.exe"; Tasks: desktopicon

[Code]
procedure SeedUserCommandTree();
var
  UserFile, DefaultFile, LinkPath: String;
  Shell, Link: Variant;
begin
  UserFile := ExpandConstant('{userappdata}\PsGuiBuilder\command-tree.json');
  DefaultFile := ExpandConstant('{app}\command-tree.json');
  if (not FileExists(UserFile)) and FileExists(DefaultFile) then
  begin
    ForceDirectories(ExpandConstant('{userappdata}\PsGuiBuilder'));
    FileCopy(DefaultFile, UserFile, False);
  end;

  if FileExists(UserFile) then
  begin
    LinkPath := ExpandConstant('{group}\Edit command tree.lnk');
    Shell := CreateOleObject('WScript.Shell');
    Link := Shell.CreateShortcut(LinkPath);
    Link.TargetPath := UserFile;
    Link.Description := 'Edit the per-user command tree. Upgrades do not replace this file.';
    Link.Save;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    SeedUserCommandTree();
end;
