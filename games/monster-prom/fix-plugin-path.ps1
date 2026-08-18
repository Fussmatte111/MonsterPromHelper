# Fix a nested plugin folder:
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
    Write-Host "No nested path found - OK: $right"
    if (Test-Path (Join-Path $right "MonsterPromHelper.Ingame.dll")) {
        Write-Host "Plugin DLL is in the right place."
    } else {
        Write-Host "WARNING: DLL missing under $right - run install-bepinex.ps1"
    }
    exit 0
}

New-Item -ItemType Directory -Force -Path $right | Out-Null
Copy-Item "$wrong\*" $right -Recurse -Force
Write-Host "Copied to: $right"
Write-Host "Optional: delete $wrong"
