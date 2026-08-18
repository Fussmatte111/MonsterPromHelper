# Build MonsterPromHelper.Ingame.dll and pack the install folder.
param(
    [string]$GameDir = $env:MONSTER_PROM_DIR
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$Project = Join-Path $Root "plugin\MonsterPromHelper.Ingame.csproj"
$Dist = Join-Path $Root "dist"
$Pack = Join-Path $Root "install-pack\BepInEx\plugins\MonsterPromHelper"
$DataRepo = Join-Path $Root "data"

if (-not $GameDir) {
    $GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Monster Prom"
}

if (-not (Test-Path (Join-Path $GameDir "Monster Prom.exe")) -and -not (Test-Path (Join-Path $GameDir "MonsterProm.exe"))) {
    Write-Warning "Monster Prom not found at: $GameDir"
    Write-Warning "Set MONSTER_PROM_DIR or -GameDir to the game folder."
}

# BepInEx.dll for the build: copy from the game folder, or download into lib/
$Lib = Join-Path $Root "lib"
New-Item -ItemType Directory -Force -Path $Lib | Out-Null
$core = Join-Path $GameDir "BepInEx\core"
$exePath = Join-Path $GameDir "MonsterProm.exe"
$useX86 = $true
if (Test-Path $exePath) {
    $bytes = [System.IO.File]::ReadAllBytes($exePath)
    $peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
    $machine = [BitConverter]::ToUInt16($bytes, $peOffset + 4)
    $useX86 = ($machine -eq 0x014C)
}

if (Test-Path (Join-Path $core "BepInEx.dll")) {
    Copy-Item (Join-Path $core "BepInEx.dll") $Lib -Force
    Write-Host "Copied BepInEx.dll from the game folder."
} elseif (-not (Test-Path (Join-Path $Lib "BepInEx.dll"))) {
    if ($useX86) {
        $zipUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x86_5.4.23.5.zip"
        $zip = Join-Path $env:TEMP "BepInEx_win_x86.zip"
    } else {
        $zipUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip"
        $zip = Join-Path $env:TEMP "BepInEx_win_x64.zip"
    }
    Write-Host "Downloading BepInEx for the build ($(if ($useX86) { 'x86' } else { 'x64' }))..."
    Invoke-WebRequest -Uri $zipUrl -OutFile $zip -UseBasicParsing
    $extract = Join-Path $env:TEMP "bepinex_build_extract"
    if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
    Expand-Archive -Path $zip -DestinationPath $extract -Force
    Copy-Item (Join-Path $extract "BepInEx\core\BepInEx.dll") $Lib -Force
    Write-Host "Downloaded BepInEx.dll into lib/."
}

$env:MONSTER_PROM_DIR = $GameDir
dotnet build $Project -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Build failed. Is BepInEx installed in the game folder?"
    Write-Host "See games/monster-prom/README.md, then run build.ps1 again."
    exit 1
}

New-Item -ItemType Directory -Force -Path (Join-Path $Pack "data") | Out-Null
Copy-Item (Join-Path $Dist "MonsterPromHelper.Ingame.dll") (Join-Path $Pack "MonsterPromHelper.Ingame.dll") -Force
Copy-Item (Join-Path $DataRepo "events_db.json") (Join-Path $Pack "data\events_db.json") -Force
Copy-Item (Join-Path $DataRepo "secret_endings.json") (Join-Path $Pack "data\secret_endings.json") -Force

Write-Host ""
Write-Host "Done:"
Write-Host "  DLL: $Dist\MonsterPromHelper.Ingame.dll"
Write-Host "  Install pack: $Root\install-pack\"
Write-Host ""
Write-Host "Copy the contents of install-pack\ into your Monster Prom folder (merge)."
