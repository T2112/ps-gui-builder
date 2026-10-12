# Personal project by Thomas Schueler.
# Developed on personal time using personal equipment.
# Not related to any employment.

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$version = (Get-Content (Join-Path $root "VERSION") -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "VERSION must be major.minor.patch. Found '$version'."
}

$publish = Join-Path $root "artifacts\publish"
if (Test-Path $publish) {
    Remove-Item $publish -Recurse -Force
}

Write-Host "Publishing PsGuiBuilder $version (self-contained win-x64)"
dotnet publish (Join-Path $root "src\PsGuiBuilder\PsGuiBuilder.csproj") `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugSymbols=false `
    -p:DebugType=None `
    --output $publish
if ($LASTEXITCODE -ne 0) { throw "Publishing PsGuiBuilder failed." }

$hostOut = Join-Path $publish "elevated-host"
Write-Host "Publishing PsGuiBuilder.ElevatedHost (self-contained win-x64)"
dotnet publish (Join-Path $root "src\PsGuiBuilder.ElevatedHost\PsGuiBuilder.ElevatedHost.csproj") `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugSymbols=false `
    -p:DebugType=None `
    --output $hostOut
if ($LASTEXITCODE -ne 0) { throw "Publishing PsGuiBuilder.ElevatedHost failed." }

@(
    (Join-Path $publish "PsGuiBuilder.exe"),
    (Join-Path $publish "command-tree.json"),
    (Join-Path $hostOut "PsGuiBuilder.ElevatedHost.exe")
) | ForEach-Object {
    if (-not (Test-Path $_)) { throw "Publish output is missing $_." }
}

Set-Content -Path (Join-Path $publish "PsGuiBuilder.installed") -Value @"
Personal project by Thomas Schueler.
Developed on personal time using personal equipment.
Not related to any employment.
Version $version
"@ -Encoding ascii

Get-ChildItem $publish -Recurse -Filter *.pdb | Remove-Item -Force

$iscc = Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) {
    $iscc = Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"
}
if (-not (Test-Path $iscc)) {
    throw "Inno Setup 6 (ISCC.exe) was not found."
}

$publishIss = $publish.Replace('\', '/')
$rootIss = "$root".Replace('\', '/')
& $iscc "/DAppVersion=$version" "/DPublishDir=$publishIss" "/DRepoRoot=$rootIss" (Join-Path $PSScriptRoot "PsGuiBuilder.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed." }

$setup = Join-Path $root "artifacts\PsGuiBuilder-Setup-$version.exe"
if (-not (Test-Path $setup)) { throw "Installer was not created at $setup." }

$hash = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLowerInvariant()
$leaf = Split-Path $setup -Leaf
Set-Content -Path "$setup.sha256" -Value "$hash  $leaf" -Encoding ascii
Write-Host "Created $setup"
Write-Host "$hash  $leaf"
