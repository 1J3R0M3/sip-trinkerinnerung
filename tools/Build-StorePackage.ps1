<#
.SYNOPSIS
  Baut das Store-Paket (.msixupload mit x64 und arm64) nach artifacts\AppPackages.

.DESCRIPTION
  Unsigniert: Der Microsoft Store signiert das Paket beim Einreichen selbst.
  Braucht nur Windows und das .NET-10-SDK; die MSIX-Werkzeuge kommen per NuGet.
  Die Paketversion steht in src\App\Package.appxmanifest (Identity/Version).
  Aufruf aus dem Repo-Wurzelverzeichnis:  .\tools\Build-StorePackage.ps1

.PARAMETER Version
  Neue Version für ein Store-Update, z. B. 1.0.1 (wird zu 1.0.1.0). Wird ins Manifest
  geschrieben und muss höher sein als die bisherige. Ohne Angabe wird die aktuelle
  Version neu gebaut (z. B. nach einem abgelehnten Upload).

.EXAMPLE
  .\tools\Build-StorePackage.ps1 -Version 1.0.1
#>
param([string]$Version)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'artifacts\AppPackages\'
$manifest = Join-Path $root 'src\App\Package.appxmanifest'

# Version im Manifest: <Identity ... Version="Major.Minor.Build.0" />
$identityVersion = '(<Identity\b[^>]*?\sVersion=")([^"]*)(")'
$text = [IO.File]::ReadAllText($manifest)
$match = [regex]::Match($text, $identityVersion)
if (-not $match.Success) { throw "Keine Identity-Version in $manifest gefunden." }
$current = [version]$match.Groups[2].Value

if ($Version) {
    if ($Version -notmatch '^\d{1,5}\.\d{1,5}\.\d{1,5}(\.0)?$') {
        throw "Ungültige Version '$Version'. Erwartet: Major.Minor.Build, z. B. 1.0.1 (letzte Stelle im Store immer 0)."
    }
    $parts = @($Version.Split('.') | ForEach-Object { [int]$_ })
    if ($parts | Where-Object { $_ -gt 65535 }) { throw "Ungültige Version '$Version': jede Stelle höchstens 65535." }
    $new = [version]::new($parts[0], $parts[1], $parts[2], 0)
    if ($new -le $current) {
        throw "Version $new ist nicht höher als die bisherige $current. Für einen Neubau derselben Version -Version weglassen."
    }
    # Kodierung (mit/ohne BOM) beibehalten.
    $bom = [IO.File]::ReadAllBytes($manifest)[0..2] -join ',' -eq '239,187,191'
    $text = $text.Substring(0, $match.Groups[2].Index) + $new + $text.Substring($match.Groups[2].Index + $match.Groups[2].Length)
    [IO.File]::WriteAllText($manifest, $text, [Text.UTF8Encoding]::new($bom))
    "Version: $current -> $new (Package.appxmanifest)"
    $current = $new
}
else {
    "Version: $current (unverändert)"
}

# dotnet fehlt in manchen Shells im PATH.
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { $env:PATH = "$env:ProgramFiles\dotnet;$env:PATH" }

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

# Immer sauber bauen: Der inkrementelle Build übernimmt Änderungen an der Paketidentität
# nicht in die Upload-Zwischenordner (Upload\ForUploadBundle), das Bundle hätte dann die alte.
foreach ($dir in 'bin', 'obj') {
    Get-ChildItem (Join-Path $root "src\App\$dir") -Directory -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName 'Release' } |
        Where-Object { Test-Path $_ } |
        ForEach-Object { Remove-Item $_ -Recurse -Force }
}

dotnet build (Join-Path $root 'src\App\App.csproj') -nologo -v:m -c Release -p:Platform=x64 `
    -p:GenerateAppxPackageOnBuild=true -p:UapAppxPackageBuildMode=StoreUpload `
    -p:AppxBundle=Always '-p:AppxBundlePlatforms=x64|arm64' `
    -p:AppxPackageSigningEnabled=false "-p:AppxPackageDir=$out"
if ($LASTEXITCODE -ne 0) { throw "Build fehlgeschlagen ($LASTEXITCODE)." }

$upload = Get-ChildItem $out -Filter *.msixupload | Select-Object -First 1
if (-not $upload) { throw 'Kein .msixupload erzeugt.' }
"Store-Paket $current`: $($upload.FullName) ($([math]::Round($upload.Length / 1MB, 1)) MB)"
