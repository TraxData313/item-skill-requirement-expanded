# Builds Gear Mastery and installs it into the game's Modules folder as the DEV module.
# Usage: powershell -ExecutionPolicy Bypass -File tools\deploy.ps1 [-Configuration Release] [-GameFolder <path>]
#
# The dev install wears its own identity - folder and Id "GearMastery.Dev", shown as
# "Gear Mastery (dev)" in the launcher. Same convention as MyTroopsGear.Dev / TraxCombatEnhancements.Dev:
# it can sit beside a Steam Workshop copy and be picked deliberately (enable only ONE of the two).
#
# (ASCII only in this file: Windows PowerShell 5.1 reads a BOM-less script as ANSI.)
param(
    [string]$Configuration = "Release",
    # Empty = read GameFolder from Directory.Build.props (and its git-ignored .user override).
    [string]$GameFolder = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$moduleName = "GearMastery"
$displayName = "Gear Mastery"
$moduleProject = Join-Path $repoRoot "src\$moduleName.Module\$moduleName.Module.csproj"

if (-not $GameFolder) {
    $GameFolder = "$(& dotnet msbuild $moduleProject -nologo -getProperty:GameFolder)".Trim()
    if ($LASTEXITCODE -ne 0 -or -not $GameFolder) { throw "Could not read GameFolder from Directory.Build.props." }
}
if (-not (Test-Path (Join-Path $GameFolder "Modules\Native\SubModule.xml"))) {
    throw "No Bannerlord install at '$GameFolder' - set GameFolder in Directory.Build.props.user or pass -GameFolder."
}

$moduleDir = Join-Path $GameFolder "Modules\$moduleName.Dev"
$binDir = Join-Path $moduleDir "bin\Win64_Shipping_Client"

# The game holds module DLLs from startup until it quits, main menu included.
if (Test-Path $binDir) {
    foreach ($dll in Get-ChildItem $binDir -Filter *.dll) {
        try { [System.IO.File]::Open($dll.FullName, 'Open', 'ReadWrite', 'None').Dispose() }
        catch { throw "$($dll.Name) is locked - Bannerlord is running. Quit the game, then deploy again." }
    }
}

dotnet build $moduleProject -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

if (Test-Path $binDir) { Remove-Item $binDir -Recurse -Force }
New-Item -ItemType Directory -Force $binDir | Out-Null
$outDir = Join-Path $repoRoot "module\bin\Win64_Shipping_Client"
foreach ($name in $moduleName, "$moduleName.Core") {
    Copy-Item (Join-Path $outDir "$name.dll") $binDir -Force
    $pdb = Join-Path $outDir "$name.pdb"
    if (Test-Path $pdb) { Copy-Item $pdb $binDir -Force }
}

# GUI and ModuleData, mirrored so a removed file goes too (Remove-then-copy: Copy-Item onto an existing
# folder would nest GUI\GUI inside it). Either may not exist yet.
foreach ($sub in "GUI", "ModuleData") {
    $dest = Join-Path $moduleDir $sub
    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
    $source = Join-Path $repoRoot "module\$sub"
    if (Test-Path $source) { Copy-Item $source $dest -Recurse }
}

# The manifest, re-badged as the dev module. Only the FIRST Id/Name (the module's own) change.
$manifest = Get-Content (Join-Path $repoRoot "module\SubModule.xml") -Raw
$devManifest = ([regex]"<Id value=""$moduleName"" />").Replace($manifest, "<Id value=""$moduleName.Dev"" />", 1)
$devManifest = ([regex]"<Name value=""$displayName"" />").Replace($devManifest, "<Name value=""$displayName (dev)"" />", 1)
if ($devManifest -notmatch "<Id value=""$moduleName.Dev"" />" -or -not $devManifest.Contains("<Name value=""$displayName (dev)"" />")) {
    throw "SubModule.xml no longer matches the expected Id/Name lines - update deploy.ps1."
}
Set-Content (Join-Path $moduleDir "SubModule.xml") $devManifest -Encoding utf8

Write-Host "Deployed to $moduleDir as '$displayName (dev)' - enable it in the launcher."
