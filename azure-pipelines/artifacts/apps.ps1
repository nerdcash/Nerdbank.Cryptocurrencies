$RepoRoot = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\..")
$BuildConfiguration = $env:BUILDCONFIGURATION
if (!$BuildConfiguration) {
    $BuildConfiguration = 'Debug'
}

$VelopackPackageDir = "$RepoRoot/bin/Packages/$BuildConfiguration/velopack"
$VelopackDataDir = "$RepoRoot/obj/src/Nerdbank.Zcash.App/Nerdbank.Zcash.App.Desktop/x64/$BuildConfiguration"

if (!(Test-Path $VelopackPackageDir))  { return }

@{
    "$VelopackPackageDir" = (Get-ChildItem $VelopackPackageDir -Recurse);
    "$VelopackDataDir" = (Get-ChildItem "$VelopackDataDir\velopack-data.json");
}
