# Removes duplicate MonsterPromHelper plugin folders (causes Overlay AN+AUS in one action).
param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Monster Prom"
)

$paths = @(
    (Join-Path $GameDir "BepInEx\plugins\MonsterPromHelper"),
    (Join-Path $GameDir "BepInEx\plugins\BepInEx\plugins\MonsterPromHelper")
)

$dllName = "MonsterPromHelper.Ingame.dll"
$found = @()

foreach ($p in $paths) {
    $dll = Join-Path $p $dllName
    if (Test-Path $dll) {
        $found += $dll
    }
}

if ($found.Count -le 1) {
    Write-Host "OK: $($found.Count) Plugin-DLL gefunden."
    foreach ($f in $found) { Write-Host "  $f" }
    exit 0
}

Write-Host "WARNUNG: $($found.Count) Kopien der Plugin-DLL - erzeugt AN+AUS im Log!"
foreach ($f in $found) { Write-Host "  $f" }

$keep = Join-Path $GameDir "BepInEx\plugins\MonsterPromHelper"
$wrong = Join-Path $GameDir "BepInEx\plugins\BepInEx\plugins\MonsterPromHelper"
if ((Test-Path (Join-Path $wrong $dllName)) -and (Test-Path (Join-Path $keep $dllName))) {
    Write-Host "Entferne verschachtelten Ordner: $wrong"
    Remove-Item $wrong -Recurse -Force
}

Write-Host "Fertig. Spiel neu starten."
