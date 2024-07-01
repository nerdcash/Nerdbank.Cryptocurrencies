$RepoRoot = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\..")
$BuildConfiguration = $env:BUILDCONFIGURATION
if (!$BuildConfiguration) {
    $BuildConfiguration = 'Debug'
}

$AppRoot = "$RepoRoot/bin/Nerdbank.Zcash.App.Android/$BuildConfiguration/net8.0-android/publish"

if (!(Test-Path $AppRoot))  { return }

@{
    "$AppRoot" = (Get-ChildItem "$AppRoot/*.aab","$AppRoot/*.apk")
}
