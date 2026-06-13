# Build MonsterPromHelper.Ingame.dll and pack install folder.
param(
    [string]$GameDir = $env:MONSTER_PROM_DIR
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$Project = Join-Path $Root "MonsterPromHelper.Ingame\MonsterPromHelper.Ingame.csproj"
$Dist = Join-Path $Root "dist"
$Pack = Join-Path $Root "install-pack\BepInEx\plugins\MonsterPromHelper"
$DataRepo = Join-Path (Split-Path $Root -Parent) "data"

if (-not $GameDir) {
    $GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Monster Prom"
}

if (-not (Test-Path (Join-Path $GameDir "Monster Prom.exe")) -and -not (Test-Path (Join-Path $GameDir "MonsterProm.exe"))) {
    Write-Warning "Monster Prom nicht gefunden unter: $GameDir"
    Write-Warning "Setze MONSTER_PROM_DIR oder -GameDir auf den Spielordner."
}

# BepInEx.dll für Build: aus Spielordner oder Download nach lib/
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
    Write-Host "BepInEx.dll aus Spielordner kopiert."
} elseif (-not (Test-Path (Join-Path $Lib "BepInEx.dll"))) {
    if ($useX86) {
        $zipUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x86_5.4.23.5.zip"
        $zip = Join-Path $env:TEMP "BepInEx_win_x86.zip"
    } else {
        $zipUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip"
        $zip = Join-Path $env:TEMP "BepInEx_win_x64.zip"
    }
    Write-Host "Lade BepInEx fuer Build ($(if ($useX86) { 'x86' } else { 'x64' }))..."
    Invoke-WebRequest -Uri $zipUrl -OutFile $zip -UseBasicParsing
    $extract = Join-Path $env:TEMP "bepinex_build_extract"
    if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
    Expand-Archive -Path $zip -DestinationPath $extract -Force
    Copy-Item (Join-Path $extract "BepInEx\core\BepInEx.dll") $Lib -Force
    Write-Host "BepInEx.dll nach lib/ geladen."
}

$env:MONSTER_PROM_DIR = $GameDir
dotnet build $Project -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Build fehlgeschlagen. Hast du BepInEx im Spielordner installiert?"
    Write-Host "Siehe bepinex-plugin\README.md Schritt 1 (BepInEx), dann erneut build.ps1"
    exit 1
}

New-Item -ItemType Directory -Force -Path (Join-Path $Pack "data") | Out-Null
Copy-Item (Join-Path $Dist "MonsterPromHelper.Ingame.dll") (Join-Path $Pack "MonsterPromHelper.Ingame.dll") -Force
Copy-Item (Join-Path $DataRepo "events_db.json") (Join-Path $Pack "data\events_db.json") -Force
Copy-Item (Join-Path $DataRepo "secret_endings.json") (Join-Path $Pack "data\secret_endings.json") -Force

Write-Host ""
Write-Host "Fertig:"
Write-Host "  DLL: $Dist\MonsterPromHelper.Ingame.dll"
Write-Host "  Install-Paket: $Root\install-pack\"
Write-Host ""
Write-Host "Kopiere den Inhalt von install-pack\ in deinen Monster-Prom-Ordner (Merge)."
