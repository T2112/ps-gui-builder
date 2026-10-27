# Personal project by Thomas Schueler.
# Developed on personal time using personal equipment.
# Not related to any employment.
#
# Build the GitHub Release body for one version: the matching CHANGELOG.md
# section, then the ownership note. The note matches
# PsGuiBuilder.Core.Updates.Changelog.OwnershipNote. The heading pattern matches
# Changelog.HeadingPattern. This script does not publish a release.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $ChangelogPath = "CHANGELOG.md",

    [string] $OutputPath
)

$ErrorActionPreference = "Stop"
$version = $Version.Trim()

function Fail([string] $Message) {
    [Console]::Error.WriteLine($Message)
    $global:LASTEXITCODE = 1
    throw $Message
}

if ($version -notmatch '^\d+(?:\.\d+)*$') {
    Fail "CHANGELOG.md has no section for $version. The release was not published."
}

if (-not (Test-Path -LiteralPath $ChangelogPath)) {
    Fail "CHANGELOG.md has no section for $version. The release was not published."
}

$changelog = Get-Content -LiteralPath $ChangelogPath -Raw
if ([string]::IsNullOrWhiteSpace($changelog)) {
    Fail "CHANGELOG.md has no section for $version. The release was not published."
}

$lines = $changelog -split "\r?\n"
$heading = '^##[ \t]+\[?v?(?<version>\d+(?:\.\d+)*)\]?(?:[ \t].*)?$'
$start = -1
$found = New-Object System.Collections.Generic.List[string]
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match $heading) {
        $headingVersion = $Matches.version
        $found.Add($headingVersion)
        if ($start -lt 0 -and $headingVersion -eq $version) {
            $start = $i
        }
    }
}

if ($start -lt 0) {
    $list = if ($found.Count -eq 0) { "(none)" } else { ($found -join ", ") }
    Fail "CHANGELOG.md has no section for $version. The release was not published. Headings found: $list"
}

$end = $lines.Count
for ($i = $start + 1; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match $heading) {
        $end = $i
        break
    }
}

$section = (($lines[$start..($end - 1)] -join "`n").Trim())
if ([string]::IsNullOrWhiteSpace($section)) {
    Fail "CHANGELOG.md has no section for $version. The release was not published."
}

$ownership = "Shellmason. Personal project by Thomas Schueler. Developed on personal time using personal equipment. Not related to any employment."
$notes = $section + "`n`n" + $ownership

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    [Console]::Out.Write($notes)
    $global:LASTEXITCODE = 0
    exit 0
}

$parent = Split-Path -Parent $OutputPath
if ($parent -and -not (Test-Path -LiteralPath $parent)) {
    New-Item -ItemType Directory -Path $parent | Out-Null
}

[System.IO.File]::WriteAllText($OutputPath, $notes)
$global:LASTEXITCODE = 0
exit 0
