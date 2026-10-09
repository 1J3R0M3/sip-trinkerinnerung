<#
.SYNOPSIS
  Baut das Store-Paket (.msixupload mit x64 und arm64) nach artifacts\AppPackages.

.DESCRIPTION
  Unsigniert: Der Microsoft Store signiert das Paket beim Einreichen selbst.
  Braucht nur Windows und das .NET-10-SDK; die MSIX-Werkzeuge kommen per NuGet.
  Die Paketversion steht in src\App\Package.appxmanifest (Identity/Version).
  Aufruf aus dem Repo-Wurzelverzeichnis:  .\tools\Build-StorePackage.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'artifacts\AppPackages\'

# dotnet fehlt in manchen Shells im PATH.
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { $env:PATH = "$env:ProgramFiles\dotnet;$env:PATH" }

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

dotnet build (Join-Path $root 'src\App\App.csproj') -nologo -v:m -c Release -p:Platform=x64 `
    -p:GenerateAppxPackageOnBuild=true -p:UapAppxPackageBuildMode=StoreUpload `
    -p:AppxBundle=Always '-p:AppxBundlePlatforms=x64|arm64' `
    -p:AppxPackageSigningEnabled=false "-p:AppxPackageDir=$out"
if ($LASTEXITCODE -ne 0) { throw "Build fehlgeschlagen ($LASTEXITCODE)." }

$upload = Get-ChildItem $out -Filter *.msixupload | Select-Object -First 1
if (-not $upload) { throw 'Kein .msixupload erzeugt.' }
"Store-Paket: $($upload.FullName) ($([math]::Round($upload.Length / 1MB, 1)) MB)"
