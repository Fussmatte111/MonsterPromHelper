# Build MonsterCampHelper.Ingame.dll and pack the install folder.
param(
    [string]$GameDir = $env:MONSTER_CAMP_DIR
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$RepoRoot = Split-Path (Split-Path $Root)
$Project = Join-Path $Root "plugin\MonsterCampHelper.Ingame.csproj"
$Dist = Join-Path $Root "dist"
$Pack = Join-Path $Root "install-pack\BepInEx\plugins\MonsterCampHelper"
$DataRepo = Join-Path $Root "data"
$Tools = Join-Path $RepoRoot "tools"

if (-not $GameDir) {
    $GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Monster Prom 2 - Monster Camp"
}

if (-not (Test-Path (Join-Path $GameDir "MonsterCamp.exe"))) {
    Write-Warning "Monster Camp not found at: $GameDir"
    Write-Warning "Set MONSTER_CAMP_DIR or -GameDir to the game folder."
}

Write-Host "Building event/drink database from the Google Sheet..."
python (Join-Path $Tools "build_camp_db.py")
if ($LASTEXITCODE -ne 0) { exit 1 }

$Lib = Join-Path $Root "lib"
New-Item -ItemType Directory -Force -Path $Lib | Out-Null
$core = Join-Path $GameDir "BepInEx\core"

if (Test-Path (Join-Path $core "BepInEx.dll")) {
    Copy-Item (Join-Path $core "BepInEx.dll") $Lib -Force
    Copy-Item (Join-Path $core "0Harmony.dll") $Lib -Force -ErrorAction SilentlyContinue
    Write-Host "Copied BepInEx.dll from the game folder."
} elseif (-not (Test-Path (Join-Path $Lib "BepInEx.dll"))) {
    $zipUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x86_5.4.23.5.zip"
    $zip = Join-Path $env:TEMP "BepInEx_win_x86_camp.zip"
    Write-Host "Downloading BepInEx x86 for the build..."
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
    Write-Host "Build failed. Is BepInEx installed in the game folder?"
    exit 1
}

New-Item -ItemType Directory -Force -Path (Join-Path $Pack "data") | Out-Null
Copy-Item (Join-Path $Dist "MonsterCampHelper.Ingame.dll") (Join-Path $Pack "MonsterCampHelper.Ingame.dll") -Force
Copy-Item (Join-Path $DataRepo "events_db.json") (Join-Path $Pack "data\events_db.json") -Force
Copy-Item (Join-Path $DataRepo "drinks_db.json") (Join-Path $Pack "data\drinks_db.json") -Force
Copy-Item (Join-Path $DataRepo "secret_endings.json") (Join-Path $Pack "data\secret_endings.json") -Force

Write-Host ""
Write-Host "Done:"
Write-Host "  DLL: $Dist\MonsterCampHelper.Ingame.dll"
Write-Host "  Install pack: $Root\install-pack\"
Write-Host ""
Write-Host "Copy install-pack\ into your Monster Camp folder (merge with BepInEx)."
