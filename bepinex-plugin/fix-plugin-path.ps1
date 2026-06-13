# Korrigiert falsch verschachtelten Plugin-Ordner:
#   BepInEx\plugins\BepInEx\plugins\MonsterPromHelper  ->  BepInEx\plugins\MonsterPromHelper
param(
    [string]$GameDir = $env:MONSTER_PROM_DIR
)

if (-not $GameDir) {
    $GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Monster Prom"
}

$wrong = Join-Path $GameDir "BepInEx\plugins\BepInEx\plugins\MonsterPromHelper"
$right = Join-Path $GameDir "BepInEx\plugins\MonsterPromHelper"

if (-not (Test-Path $wrong)) {
    Write-Host "Kein falscher Pfad gefunden - OK: $right"
    if (Test-Path (Join-Path $right "MonsterPromHelper.Ingame.dll")) {
        Write-Host "Plugin DLL liegt korrekt."
    } else {
        Write-Host "WARNUNG: DLL fehlt unter $right - install-bepinex.ps1 ausfuehren"
    }
    exit 0
}

New-Item -ItemType Directory -Force -Path $right | Out-Null
Copy-Item "$wrong\*" $right -Recurse -Force
Write-Host "Kopiert nach: $right"
Write-Host "Optional loeschen: $wrong"
