$RepoRoot = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\..")
$BuildConfiguration = $env:BUILDCONFIGURATION
if (!$BuildConfiguration) {
    $BuildConfiguration = 'Debug'
}

$AppRoot = "$RepoRoot/bin/Nerdbank.Zcash.App.iOS/$BuildConfiguration/net8.0-ios/ios-arm64/publish"

if (!(Test-Path $AppRoot)) { return }

@{
    "$AppRoot" = Get-ChildItem $AppRoot
}
