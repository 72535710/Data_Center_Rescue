[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$UnityPath = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ExpectedEditor = "6000.6.0f1"

if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $candidate = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$ExpectedEditor/Editor/Unity.exe"
    if (Test-Path $candidate) { $UnityPath = $candidate }
}
if ([string]::IsNullOrWhiteSpace($UnityPath) -or -not (Test-Path $UnityPath)) {
    throw "No se encontró Unity.exe. Pasa -UnityPath con la ruta de Unity $ExpectedEditor, por ejemplo: -UnityPath 'C:\Program Files\Unity\Hub\Editor\$ExpectedEditor\Editor\Unity.exe'."
}

$LogPath = Join-Path $PSScriptRoot "CompileCheck.log"
if (Test-Path $LogPath) { Remove-Item $LogPath -Force }

Write-Host "Proyecto: $ProjectRoot"
Write-Host "Editor:   $UnityPath"
Write-Host "Prueba:   importación y compilación de assemblies mediante Unity en batchmode"
Write-Host "Unity se cerrará al terminar; no abras el mismo proyecto en otra instancia."

$arguments = @(
    "-batchmode",
    "-nographics",
    "-projectPath", ('"' + $ProjectRoot + '"'),
    "-executeMethod", "DataCenterRescue.Editor.DCRProjectSetup.RunBatchCompileCheck",
    "-logFile", ('"' + $LogPath + '"'),
    "-quit"
)
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -Wait -PassThru -NoNewWindow

if (-not (Test-Path $LogPath)) {
    throw "Unity no generó el log de compilación. Código de salida: $($process.ExitCode)"
}
$log = Get-Content -Path $LogPath -Raw
$compileErrors = [regex]::Matches($log, '(?im)^.*error CS[0-9]{4}.*$')
$compileFailure = $log -match '(?i)Scripts have compiler errors|Compilation failed|DCR_BATCH_COMPILE_VALIDATION_FAILED'
$successMarker = $log -match 'DCR_BATCH_COMPILE_VALIDATION_PASSED'

if ($compileErrors.Count -gt 0) {
    Write-Host "Se encontraron errores C#:" -ForegroundColor Red
    foreach ($match in $compileErrors) { Write-Host $match.Value -ForegroundColor Red }
}
if ($process.ExitCode -ne 0 -or $compileFailure -or -not $successMarker -or $compileErrors.Count -gt 0) {
    Write-Host "COMPILACIÓN NO SUPERADA. Revisa: $LogPath" -ForegroundColor Red
    exit 1
}

Write-Host "COMPILACIÓN DE UNITY SUPERADA." -ForegroundColor Green
Write-Host "Marcador: $($log -split "`n" | Where-Object { $_ -match 'DCR_BATCH_COMPILE_VALIDATION_PASSED' } | Select-Object -First 1)"
Write-Host "Log completo: $LogPath"
exit 0
