$RepoRoot = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\..")
$BuildConfiguration = $env:BUILDCONFIGURATION
if (!$BuildConfiguration) {
    $BuildConfiguration = 'Debug'
}

$PublishRoot = "$RepoRoot/bin/publish/$BuildConfiguration"
$VelopackDataDir = "$RepoRoot/obj/src/Nerdbank.Zcash.App/Nerdbank.Zcash.App.Desktop/x64/$BuildConfiguration"

if (!(Test-Path $PublishRoot))  { return }

@{
    "$PublishRoot" = (Get-ChildItem $PublishRoot -Recurse);
    "$VelopackDataDir" = (Get-ChildItem "$VelopackDataDir\velopack-data.json");
}
