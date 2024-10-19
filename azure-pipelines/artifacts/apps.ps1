$RepoRoot = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\..")
$BuildConfiguration = $env:BUILDCONFIGURATION
if (!$BuildConfiguration) {
    $BuildConfiguration = 'Debug'
}

$VelopackPackageDir = "$RepoRoot/bin/Packages/$BuildConfiguration/velopack"
if (!(Test-Path $VelopackPackageDir)) { return }
$VelopackBasePath = "$RepoRoot/obj/src/Nerdbank.Zcash.App/Nerdbank.Zcash.App.Desktop/$BuildConfiguration/net8.0-windows10.0.22621.0"
if (!(Test-Path $VelopackBasePath)) { $VelopackBasePath = Join-Path (Split-Path $VelopackBasePath) 'net8.0-macos' }
if (!(Test-Path $VelopackBasePath)) { $VelopackBasePath = Join-Path (Split-Path $VelopackBasePath) 'net8.0' }
if (!(Test-Path $VelopackBasePath)) { return }
$VelopackFullPaths = (Get-ChildItem -Recurse "$VelopackBasePath/velopack-data.json")
if (!$VelopackFullPaths)  { return }
$VelopackFullPath = $VelopackFullPaths[0]
$VelopackDataDir = Split-Path $VelopackFullPath

@{
    "$VelopackPackageDir" = (Get-ChildItem $VelopackPackageDir -Recurse);
    "$VelopackDataDir" = $VelopackFullPath;
}
