<#
.SYNOPSIS
  Build and run the Atlas viewer, optionally publishing a self-contained build first.

.EXAMPLE
  .\tools\run-atlas.ps1
  Builds and runs the desktop viewer from source.

.EXAMPLE
  .\tools\run-atlas.ps1 -Publish -Rid win-x64
  Publishes a standalone build and launches the published exe.
#>
[CmdletBinding()]
param(
    [switch] $Publish,
    [string] $Rid = 'win-x64',
    [string] $Tfm = 'net10.0-desktop'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'Atlas.App\Atlas.App.csproj'

if ($Publish) {
    Write-Host "Publishing self-contained Atlas viewer ($Rid)..." -ForegroundColor Cyan
    dotnet publish $project -f $Tfm -c Release -r $Rid --self-contained
    $exe = Join-Path $root "Atlas.App\bin\Release\$Tfm\$Rid\publish\Atlas.App.exe"
    Write-Host "Launching $exe" -ForegroundColor Green
    & $exe
}
else {
    Write-Host "Running Atlas viewer from source..." -ForegroundColor Cyan
    dotnet run -f $Tfm --project $project
}
