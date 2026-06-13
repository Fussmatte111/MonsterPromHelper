# Build MonsterProm4Helper.Ingame.dll and pack install folder for Monster Prom 4.
param(
    [string]$GameDir = $env:MONSTER_CON_DIR
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$Project = Join-Path $Root "MonsterProm4Helper.Ingame\MonsterProm4Helper.Ingame.csproj"
$Dist = Join-Path $Root "dist-mp4"
$Pack = Join-Path $Root "install-pack-mp4\BepInEx\plugins\MonsterProm4Helper"
$DataRepo = Join-Path (Split-Path $Root -Parent) "data-mp4"
$BuildDb = Join-Path (Split-Path $Root -Parent) "tools\build_mp4_event_db.py"
$BuildPregame = Join-Path (Split-Path $Root -Parent) "tools\build_mp4_pregame_wiki.py"

if (-not $GameDir) {
    $GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Monster Prom 4 - Monster Con"
}

if (-not (Test-Path (Join-Path $GameDir "MonsterCon.exe"))) {
    Write-Warning "Monster Prom 4 nicht gefunden unter: $GameDir"
    Write-Warning "Setze MONSTER_CON_DIR oder -GameDir auf den Spielordner."
}

$Lib = Join-Path $Root "lib"
New-Item -ItemType Directory -Force -Path $Lib | Out-Null
$core = Join-Path $GameDir "BepInEx\core"
$exePath = Join-Path $GameDir "MonsterCon.exe"
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
        $zip = Join-Path $env:TEMP "BepInEx_win_x86_mp4.zip"
    } else {
        $zipUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip"
        $zip = Join-Path $env:TEMP "BepInEx_win_x64_mp4.zip"
    }
    Write-Host "Lade BepInEx fuer Build ($(if ($useX86) { 'x86' } else { 'x64' }))..."
    Invoke-WebRequest -Uri $zipUrl -OutFile $zip -UseBasicParsing
    $extract = Join-Path $env:TEMP "bepinex_build_extract_mp4"
    if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
    Expand-Archive -Path $zip -DestinationPath $extract -Force
    Copy-Item (Join-Path $extract "BepInEx\core\BepInEx.dll") $Lib -Force
    Write-Host "BepInEx.dll nach lib/ geladen."
}

$env:MONSTER_CON_DIR = $GameDir

if (Test-Path $BuildDb) {
    Write-Host "Baue MP4 Event-DB aus Steam-Guide..."
    python $BuildDb
}

if (Test-Path $BuildPregame) {
    Write-Host "Baue MP4 Pregame-DB aus Wiki..."
    python $BuildPregame
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Pregame-DB Build fehlgeschlagen - vorhandene JSON-Dateien werden verwendet."
    }
}

dotnet build $Project -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Build fehlgeschlagen. BepInEx im MP4-Ordner installiert?"
    exit 1
}

New-Item -ItemType Directory -Force -Path (Join-Path $Pack "data") | Out-Null
Copy-Item (Join-Path $Dist "MonsterProm4Helper.Ingame.dll") (Join-Path $Pack "MonsterProm4Helper.Ingame.dll") -Force
Copy-Item (Join-Path $DataRepo "secret_endings.json") (Join-Path $Pack "data\secret_endings.json") -Force
if (Test-Path (Join-Path $DataRepo "events_db.json")) {
    Copy-Item (Join-Path $DataRepo "events_db.json") (Join-Path $Pack "data\events_db.json") -Force
}
if (Test-Path (Join-Path $DataRepo "pregame_db.json")) {
    Copy-Item (Join-Path $DataRepo "pregame_db.json") (Join-Path $Pack "data\pregame_db.json") -Force
}

$GamePlugin = Join-Path $GameDir "BepInEx\plugins\MonsterProm4Helper"
if (Test-Path (Join-Path $GameDir "MonsterCon.exe")) {
    New-Item -ItemType Directory -Force -Path (Join-Path $GamePlugin "data") | Out-Null
    Copy-Item (Join-Path $Dist "MonsterProm4Helper.Ingame.dll") (Join-Path $GamePlugin "MonsterProm4Helper.Ingame.dll") -Force
    Copy-Item (Join-Path $DataRepo "secret_endings.json") (Join-Path $GamePlugin "data\secret_endings.json") -Force
    if (Test-Path (Join-Path $DataRepo "events_db.json")) {
        Copy-Item (Join-Path $DataRepo "events_db.json") (Join-Path $GamePlugin "data\events_db.json") -Force
    }
    if (Test-Path (Join-Path $DataRepo "pregame_db.json")) {
        Copy-Item (Join-Path $DataRepo "pregame_db.json") (Join-Path $GamePlugin "data\pregame_db.json") -Force
    }
    Write-Host ""
    Write-Host "Automatisch ins Spiel kopiert:"
    Write-Host "  $GamePlugin"
} else {
    Write-Host ""
    Write-Host "Spielordner nicht gefunden - nur install-pack-mp4 aktualisiert."
}

Write-Host ""
Write-Host "Fertig (Monster Prom 4):"
Write-Host "  DLL: $Dist\MonsterProm4Helper.Ingame.dll"
Write-Host "  Install-Paket: $Root\install-pack-mp4\"
Write-Host ""
Write-Host "Spiel neu starten, oben links F8 Overlay pruefen, dann F8 druecken."
