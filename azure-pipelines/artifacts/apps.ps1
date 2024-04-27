$RepoRoot = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\..")
$BuildConfiguration = $env:BUILDCONFIGURATION
if (!$BuildConfiguration) {
    $BuildConfiguration = 'Debug'
}

$VelopackPackageDir = "$RepoRoot/bin/Packages/$BuildConfiguration/velopack"
if (!(Test-Path $VelopackPackageDir)) { return }
$VelopackBasePath = "$RepoRoot/obj/src/Nerdbank.Zcash.App/Nerdbank.Zcash.App.Desktop/x64/$BuildConfiguration/net8.0"
if (!(Test-Path $VelopackBasePath)) { return }
$VelopackFullPaths = (Get-ChildItem -Recurse "$VelopackBasePath/velopack-data.json")
if (!$VelopackFullPaths)  { return }
$VelopackFullPath = $VelopackFullPaths[0]
$VelopackDataDir = Split-Path $VelopackFullPath

@{
    "$VelopackPackageDir" = (Get-ChildItem $VelopackPackageDir -Recurse);
    "$VelopackDataDir" = $VelopackFullPath;
}
