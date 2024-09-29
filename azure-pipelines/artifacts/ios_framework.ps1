$RepoRoot = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\..")
$BuildConfiguration = $env:BUILDCONFIGURATION
if (!$BuildConfiguration) {
    $BuildConfiguration = 'Debug'
}

$BuildConfiguration = $BuildConfiguration.ToLower()
$FrameworkRoot = "$RepoRoot/bin/$BuildConfiguration/nerdbank_zcash_rust.xcframework"

if (!(Test-Path $FrameworkRoot))  { return }

@{
    "$FrameworkRoot" = (Get-ChildItem $FrameworkRoot -Recurse)
}
