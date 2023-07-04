#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Builds the rust dynamic link library for all supported targets.
.PARAMETER Release
    Build in release mode.
#>

[CmdletBinding(SupportsShouldProcess = $true)]
Param(
    [switch]$Release
)

$buildArgs = @()
if ($Release) {
    $buildArgs += '-r'
}

Push-Location $PSScriptRoot

$rustTargets = @(..\..\azure-pipelines\Get-RustTargets.ps1)
$rustTargets | % { $buildArgs += "--target=$_" }

cargo build @buildArgs

$configPathSegment = 'debug'
if ($Release) { $configPathSegment = 'release' }
$rustTargets | % {
    New-Item -ItemType Directory -Path "..\..\obj\src\nerdbank-zcash-rust\$configPathSegment\$_" -Force | Out-Null
    Copy-Item "target/$_/$configPathSegment/*nerdbank_zcash_rust*" "..\..\obj\src\nerdbank-zcash-rust\$configPathSegment\$_"
}

Pop-Location
