# Build MonsterProm4Helper.Ingame.dll and pack the install folder for Monster Con.
param(
    [string]$GameDir = $env:MONSTER_CON_DIR
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$RepoRoot = Split-Path (Split-Path $Root)
$Project = Join-Path $Root "plugin\MonsterProm4Helper.Ingame.csproj"
$Dist = Join-Path $Root "dist"
$Pack = Join-Path $Root "install-pack\BepInEx\plugins\MonsterProm4Helper"
$DataRepo = Join-Path $Root "data"
$BuildDb = Join-Path $RepoRoot "tools\build_mp4_event_db.py"
$BuildPregame = Join-Path $RepoRoot "tools\build_mp4_pregame_wiki.py"

if (-not $GameDir) {
    $GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Monster Prom 4 - Monster Con"
}

if (-not (Test-Path (Join-Path $GameDir "MonsterCon.exe"))) {
    Write-Warning "Monster Prom 4 not found at: $GameDir"
    Write-Warning "Set MONSTER_CON_DIR or -GameDir to the game folder."
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
    Write-Host "Copied BepInEx.dll from the game folder."
} elseif (-not (Test-Path (Join-Path $Lib "BepInEx.dll"))) {
    if ($useX86) {
        $zipUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x86_5.4.23.5.zip"
        $zip = Join-Path $env:TEMP "BepInEx_win_x86_mp4.zip"
    } else {
        $zipUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip"
        $zip = Join-Path $env:TEMP "BepInEx_win_x64_mp4.zip"
    }
    Write-Host "Downloading BepInEx for the build ($(if ($useX86) { 'x86' } else { 'x64' }))..."
    Invoke-WebRequest -Uri $zipUrl -OutFile $zip -UseBasicParsing
    $extract = Join-Path $env:TEMP "bepinex_build_extract_mp4"
    if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
    Expand-Archive -Path $zip -DestinationPath $extract -Force
    Copy-Item (Join-Path $extract "BepInEx\core\BepInEx.dll") $Lib -Force
    Write-Host "Downloaded BepInEx.dll into lib/."
}

$env:MONSTER_CON_DIR = $GameDir

if (Test-Path $BuildDb) {
    Write-Host "Building MP4 event DB from the Steam guide..."
    python $BuildDb
}

if (Test-Path $BuildPregame) {
    Write-Host "Building MP4 pregame DB from the wiki..."
    python $BuildPregame
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Pregame DB build failed — using existing JSON files."
    }
}

dotnet build $Project -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Build failed. Is BepInEx installed in the Monster Con folder?"
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
    Write-Host "Copied into the game folder:"
    Write-Host "  $GamePlugin"
} else {
    Write-Host ""
    Write-Host "Game folder not found — only install-pack was updated."
}

Write-Host ""
Write-Host "Done (Monster Prom 4):"
Write-Host "  DLL: $Dist\MonsterProm4Helper.Ingame.dll"
Write-Host "  Install pack: $Root\install-pack\"
Write-Host ""
Write-Host "Restart the game, check F8 in the top-left, then press F8."
