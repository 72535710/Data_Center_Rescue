$ErrorActionPreference = 'Stop'

# Run this only with the Unity Editor closed. It removes only the project's
# local Burst compilation cache; it does not touch Assets, Packages or scenes.
$unityProcesses = Get-Process -Name Unity -ErrorAction SilentlyContinue
if ($unityProcesses) {
    Write-Error 'Cierra todas las instancias de Unity Editor antes de limpiar la caché de Burst.'
    exit 1
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$burstCache = Join-Path $projectRoot 'Library/BurstCache'

if (Test-Path -LiteralPath $burstCache) {
    Remove-Item -LiteralPath $burstCache -Recurse -Force
    Write-Host "Caché de Burst eliminada: $burstCache" -ForegroundColor Green
} else {
    Write-Host 'No existe Library/BurstCache; no había nada que limpiar.' -ForegroundColor Yellow
}

Write-Host 'Abre nuevamente el proyecto desde Unity Hub. Burst regenerará la caché cuando sea necesaria.'
