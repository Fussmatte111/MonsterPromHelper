# Install the correct BepInEx (32-bit!) and the helper plugin into the game folder.
param(
    [string]$GameDir = $env:MONSTER_PROM_DIR
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$DataRepo = Join-Path $Root "data"

if (-not $GameDir) {
    $GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Monster Prom"
}

$exe = Join-Path $GameDir "MonsterProm.exe"
if (-not (Test-Path $exe)) {
    Write-Error "MonsterProm.exe not found in: $GameDir"
}

# PE machine: 0x14c = 32-bit, 0x8664 = 64-bit
$bytes = [System.IO.File]::ReadAllBytes($exe)
$peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
$machine = [BitConverter]::ToUInt16($bytes, $peOffset + 4)
$is32 = ($machine -eq 0x014C)

if ($is32) {
    $zipName = "BepInEx_win_x86_5.4.23.5.zip"
    $zipUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x86_5.4.23.5.zip"
    Write-Host "Monster Prom is 32-bit -> installing BepInEx win-x86."
} else {
    $zipName = "BepInEx_win_x64_5.4.23.5.zip"
    $zipUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip"
    Write-Host "Monster Prom is 64-bit -> installing BepInEx win-x64."
}

$zip = Join-Path $env:TEMP $zipName
if (-not (Test-Path $zip)) {
    Write-Host "Downloading $zipName ..."
    Invoke-WebRequest -Uri $zipUrl -OutFile $zip -UseBasicParsing
}

$extract = Join-Path $env:TEMP "bepinex_install_extract"
if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
Expand-Archive -Path $zip -DestinationPath $extract -Force

Write-Host "Copying into: $GameDir"
Copy-Item (Join-Path $extract "winhttp.dll") $GameDir -Force
Copy-Item (Join-Path $extract "doorstop_config.ini") $GameDir -Force
if (Test-Path (Join-Path $extract ".doorstop_version")) {
    Copy-Item (Join-Path $extract ".doorstop_version") $GameDir -Force
}
Copy-Item (Join-Path $extract "BepInEx") $GameDir -Recurse -Force

# Plugin + data
& (Join-Path $Root "build.ps1") -GameDir $GameDir

$pluginDest = Join-Path $GameDir "BepInEx\plugins\MonsterPromHelper"
New-Item -ItemType Directory -Force -Path (Join-Path $pluginDest "data") | Out-Null
$src = Join-Path $Root "install-pack\BepInEx\plugins\MonsterPromHelper"
if (-not (Test-Path $src)) {
    $src = Join-Path $Root "dist"
    Copy-Item (Join-Path $Root "dist\MonsterPromHelper.Ingame.dll") (Join-Path $pluginDest "MonsterPromHelper.Ingame.dll") -Force
    Copy-Item (Join-Path $DataRepo "events_db.json") (Join-Path $pluginDest "data\events_db.json") -Force
    Copy-Item (Join-Path $DataRepo "secret_endings.json") (Join-Path $pluginDest "data\secret_endings.json") -Force
} else {
    Copy-Item "$src\*" $pluginDest -Recurse -Force
}

# Remove a nested copy if install-pack\ was copied into the game folder by mistake
$nested = Join-Path $GameDir "BepInEx\plugins\BepInEx"
if (Test-Path $nested) {
    Remove-Item $nested -Recurse -Force
    Write-Host "Removed leftover nested folder BepInEx\plugins\BepInEx."
}

Write-Host ""
Write-Host "Done. Launch MonsterProm.exe (not just the Steam overlay)."
Write-Host "After that you should see:"
Write-Host "  BepInEx\LogOutput.log"
Write-Host "  BepInEx\config\"
Write-Host "  BepInEx\cache\"
Write-Host "  BepInEx\plugins\MonsterPromHelper\"
Write-Host ""
Write-Host "In the log: 'Monster Prom Helper (Ingame)'. In-game: F8 = overlay."
