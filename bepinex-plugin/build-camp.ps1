# Build MonsterCampHelper.Ingame.dll and pack install folder.
param(
    [string]$GameDir = $env:MONSTER_CAMP_DIR
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$Project = Join-Path $Root "MonsterCampHelper.Ingame\MonsterCampHelper.Ingame.csproj"
$Dist = Join-Path $Root "dist-camp"
$Pack = Join-Path $Root "install-pack-camp\BepInEx\plugins\MonsterCampHelper"
$DataRepo = Join-Path (Split-Path $Root -Parent) "data-camp"
$Tools = Join-Path (Split-Path $Root -Parent) "tools"

if (-not $GameDir) {
    $GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Monster Prom 2 - Monster Camp"
}

if (-not (Test-Path (Join-Path $GameDir "MonsterCamp.exe"))) {
    Write-Warning "Monster Camp nicht gefunden unter: $GameDir"
    Write-Warning "Setze MONSTER_CAMP_DIR oder -GameDir auf den Spielordner."
}

Write-Host "Baue Event/Drink-Datenbank aus Google Sheet..."
python (Join-Path $Tools "build_camp_db.py")
if ($LASTEXITCODE -ne 0) { exit 1 }

$Lib = Join-Path $Root "lib"
New-Item -ItemType Directory -Force -Path $Lib | Out-Null
$core = Join-Path $GameDir "BepInEx\core"

if (Test-Path (Join-Path $core "BepInEx.dll")) {
    Copy-Item (Join-Path $core "BepInEx.dll") $Lib -Force
    Copy-Item (Join-Path $core "0Harmony.dll") $Lib -Force -ErrorAction SilentlyContinue
    Write-Host "BepInEx.dll aus Spielordner kopiert."
} elseif (-not (Test-Path (Join-Path $Lib "BepInEx.dll"))) {
    $zipUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x86_5.4.23.5.zip"
    $zip = Join-Path $env:TEMP "BepInEx_win_x86_camp.zip"
    Write-Host "Lade BepInEx x86 fuer Build..."
    Invoke-WebRequest -Uri $zipUrl -OutFile $zip -UseBasicParsing
    $extract = Join-Path $env:TEMP "bepinex_camp_extract"
    if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
    Expand-Archive -Path $zip -DestinationPath $extract -Force
    Copy-Item (Join-Path $extract "BepInEx\core\BepInEx.dll") $Lib -Force
    Copy-Item (Join-Path $extract "BepInEx\core\0Harmony.dll") $Lib -Force
}

$env:MONSTER_CAMP_DIR = $GameDir
dotnet build $Project -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Build fehlgeschlagen. BepInEx im Spielordner installiert?"
    exit 1
}

New-Item -ItemType Directory -Force -Path (Join-Path $Pack "data") | Out-Null
Copy-Item (Join-Path $Dist "MonsterCampHelper.Ingame.dll") (Join-Path $Pack "MonsterCampHelper.Ingame.dll") -Force
Copy-Item (Join-Path $DataRepo "events_db.json") (Join-Path $Pack "data\events_db.json") -Force
Copy-Item (Join-Path $DataRepo "drinks_db.json") (Join-Path $Pack "data\drinks_db.json") -Force
Copy-Item (Join-Path $DataRepo "secret_endings.json") (Join-Path $Pack "data\secret_endings.json") -Force

Write-Host ""
Write-Host "Fertig:"
Write-Host "  DLL: $Dist\MonsterCampHelper.Ingame.dll"
Write-Host "  Install-Paket: $Root\install-pack-camp\"
Write-Host ""
Write-Host "Kopiere install-pack-camp\ in deinen Monster-Camp-Ordner (Merge mit BepInEx)."
