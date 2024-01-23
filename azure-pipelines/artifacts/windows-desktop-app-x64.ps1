$RepoRoot = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\..")
$BuildConfiguration = $env:BUILDCONFIGURATION
if (!$BuildConfiguration) {
    $BuildConfiguration = 'Debug'
}

$AppRoot = "$RepoRoot/bin/Nerdbank.Zcash.App.Desktop/x64/$BuildConfiguration/net8.0-windows10.0.22621.0"

if (!(Test-Path $AppRoot))  { return }

@{
    "$AppRoot" = (Get-ChildItem $AppRoot -Recurse)
}
