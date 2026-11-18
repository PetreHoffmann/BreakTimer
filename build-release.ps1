# Erstellt einen Release-Build und legt ihn unter .\dist\BreakTimer ab (zusätzlich als ZIP).
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
dotnet build src\BreakTimer.App\BreakTimer.App.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build fehlgeschlagen' }
$out = Join-Path $PSScriptRoot 'dist\BreakTimer'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory $out | Out-Null
Copy-Item 'src\BreakTimer.App\bin\Release\net48\*' $out -Recurse -Exclude '*.pdb'
# Nicht benötigte Native-Dateien (macOS/ARM) entfernen
Get-ChildItem $out -Filter '*.dylib' -Recurse | Remove-Item -Force
if (Test-Path (Join-Path $out 'arm64')) { Remove-Item (Join-Path $out 'arm64') -Recurse -Force }
Compress-Archive -Path "$out\*" -DestinationPath (Join-Path $PSScriptRoot 'dist\BreakTimer.zip') -Force
Write-Host "Fertig: $out"
